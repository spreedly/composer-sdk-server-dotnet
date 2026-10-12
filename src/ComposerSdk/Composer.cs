using Newtonsoft.Json;

namespace Spreedly.ComposerSdk;

/// <summary>
/// Main Composer SDK client
/// </summary>
public class ComposerClient
{
    private readonly string _secretKey;
    private readonly ComposerConfig _config;
    private readonly int _requestTimeoutMs;

    public ComposerClient(string secretKey, ComposerConfig? config = null)
    {
        if (Utilities.IsNullOrEmpty(secretKey))
        {
            throw new MissingConfigException("secretKey");
        }

        _secretKey = secretKey;
        _config = config ?? new ComposerConfig();

        // Validate API version
        if (_config.ApiVersion != ApiVersion.V1)
        {
            throw new InvalidConfigException("apiVersion", _config.ApiVersion);
        }

        // Set logger filter level
        Logger.FilterLevel = Logger.LogLevelToSeverity(_config.LogLevel);

        // Resolved after the logger is configured so the message below respects LogLevel.
        // A non-positive timeout would fail every request the SDK makes.
        _requestTimeoutMs = _config.RequestTimeoutMs;
        if (_requestTimeoutMs <= 0)
        {
            Logger.Error($"config.RequestTimeoutMs must be a positive integer. Ignoring {_requestTimeoutMs} and using the {Constants.DefaultRequestTimeoutMs}ms default.").Log();
            _requestTimeoutMs = Constants.DefaultRequestTimeoutMs;
        }
    }

    /// <summary>
    /// Send a tracking event to Composer
    /// </summary>
    public async Task EventAsync(
        TrackEvent trackEvent,
        string? sessionId = null,
        string? userId = null,
        string? sourceToken = null)
    {
        if (trackEvent == null)
        {
            SkipEvent("no event was provided");
            return;
        }

        if (string.IsNullOrWhiteSpace(trackEvent.Type))
        {
            SkipEvent("event.type is missing");
            return;
        }

        if (string.IsNullOrWhiteSpace(sessionId) && string.IsNullOrWhiteSpace(sourceToken))
        {
            SkipEvent("Must provide either a sessionId or sourceToken");
            return;
        }

        // Don't send the event if SDK is disabled
        if (!_config.IsEnabled)
        {
            return;
        }

        // Set event time if not provided
        trackEvent.EventTime ??= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var url = $"{Utilities.ConstructApiUrl(_config.ApiUrl, _config.ApiVersion)}track";
        var headers = Utilities.ConstructApiHeaders(_secretKey, null, sourceToken, userId, sessionId);

        using var response = await Utilities.MakeRequestAsync(url, HttpMethod.Post, headers, trackEvent, _requestTimeoutMs);
        // MakeRequestAsync already logged a request that never got a response.
        if (response is not Utilities.RequestFailedResponse && !response.IsSuccessStatusCode)
        {
            Logger.Error($"Composer event was not accepted by the API (HTTP {(int)response.StatusCode}).").Log();
        }
    }

    /// <summary>
    /// Alias for Event (backward compatibility)
    /// </summary>
    public Task NotifyAsync(TrackEvent trackEvent, string? sessionId = null, string? userId = null, string? sourceToken = null)
        => EventAsync(trackEvent, sessionId, userId, sourceToken);

    /// <summary>
    /// Alias for Event (backward compatibility)
    /// </summary>
    public Task TrackAsync(TrackEvent trackEvent, string? sessionId = null, string? userId = null, string? sourceToken = null)
        => EventAsync(trackEvent, sessionId, userId, sourceToken);

    /// <summary>
    /// Submit a checkpoint and poll for verification if needed
    /// </summary>
    public async Task<CheckpointResponse> CheckpointAsync(
        string checkpointName,
        CheckpointEvent checkpointEvent,
        string? sessionId = null,
        string? userId = null,
        string? sourceToken = null,
        string? useVerificationId = null,
        CheckpointResponseOptions? options = null)
    {
        // Validate required parameters
        if (string.IsNullOrWhiteSpace(checkpointName))
        {
            throw new MissingParameterException("checkpointName");
        }

        if (checkpointEvent == null)
        {
            throw new MissingParameterException("event");
        }

        if (string.IsNullOrWhiteSpace(sessionId) && string.IsNullOrWhiteSpace(sourceToken))
        {
            throw new MissingParameterException("Either sessionId or sourceToken is required");
        }

        // Return approved response if SDK is disabled
        if (!_config.IsEnabled)
        {
            return new CheckpointResponse
            {
                Success = true,
                Errors = new List<ApiError>(),
                Version = ApiVersion.V1,
                Verification = new Verification
                {
                    Id = "COMPOSER_IS_DISABLED",
                    Status = VerificationStatus.Complete,
                    Outcome = VerificationOutcome.Approved,
                    StepData = new Dictionary<string, object>()
                },
                IsTimeout = false
            };
        }

        options ??= new CheckpointResponseOptions();
        var checkpointTimeout = options.Timeout > 0 ? options.Timeout.Value : Constants.BaseCheckpointMaxTimeoutMs;
        var currentPollingWait = Constants.BaseCheckpointPollingWaitMs;
        var hasNeverPolled = true;
        var numFailures = 0;
        var checkpointStartTime = DateTime.UtcNow;

        // Prepare checkpoint request
        var url = $"{Utilities.ConstructApiUrl(_config.ApiUrl, _config.ApiVersion)}checkpoint";
        var headers = Utilities.ConstructApiHeaders(_secretKey, useVerificationId, sourceToken, userId, sessionId);

        var eventBody = new Dictionary<string, object?>
        {
            ["type"] = checkpointName,
            ["data"] = checkpointEvent.Data
        };
        if (!string.IsNullOrEmpty(checkpointEvent.Ip))
        {
            eventBody["ip"] = checkpointEvent.Ip;
        }

        var requestBody = new
        {
            @event = eventBody,
            options = new
            {
                sync = options.Sync ?? false
            }
        };

        // Submit initial checkpoint request
        var httpResponse = await Utilities.MakeRequestAsync(url, HttpMethod.Post, headers, requestBody, _requestTimeoutMs);
        var responseBody = await httpResponse.Content.ReadAsStringAsync();
        var response = Utilities.ParseCheckpointResponse(responseBody);

        if (response == null)
        {
            // The body is deliberately not logged
            Logger.Error("The Composer API returned a response that is not valid JSON.").Log();
            return Utilities.ParseCheckpointResponse(Utilities.RequestFailedBody)!;
        }

        if (!response.Success)
        {
            return response;
        }

        if (Logger.IsTraceEnabled())
        {
            Logger.Trace($"Initial checkpoint response: {Utilities.RedactSensitiveFields(response)}").Log();
        }

        // Only poll PENDING; BLOCKED waits on a client action such as MFA.
        var isResolved = response.Verification?.Status != VerificationStatus.Pending;
        var verificationId = response.Verification?.Id;

        // Poll for verification result if needed
        while (!isResolved &&
               ((DateTime.UtcNow - checkpointStartTime).TotalMilliseconds < checkpointTimeout || hasNeverPolled) &&
               numFailures < Constants.MaxFailureCount)
        {
            await Utilities.SleepAsync(currentPollingWait);
            currentPollingWait = Math.Min(currentPollingWait * 2, Constants.MaxPollingWaitMs);
            hasNeverPolled = false;

            // Poll verification endpoint
            var pollUrl = $"{Utilities.ConstructApiUrl(_config.ApiUrl, _config.ApiVersion)}verification/{verificationId}";
            var pollResponse = await Utilities.MakeRequestAsync(pollUrl, HttpMethod.Get, headers, requestTimeoutMs: _requestTimeoutMs);
            var pollBody = await pollResponse.Content.ReadAsStringAsync();
            response = Utilities.ParseCheckpointResponse(pollBody);
            if (response == null)
            {
                Logger.Error("The Composer API returned a response that is not valid JSON.").Log();
            }

            if (response != null && response.Success && response.Verification != null)
            {
                var status = response.Verification.Status;
                if (string.IsNullOrEmpty(status))
                {
                    numFailures++;
                }
                else
                {
                    isResolved = status != VerificationStatus.Pending;
                    numFailures = 0; // Reset on successful response
                }
            }
            else
            {
                numFailures++;
            }
        }

        // Handle max failures
        if (numFailures >= Constants.MaxFailureCount)
        {
            Logger.Error("Service Unavailable: Maximum retry count exceeded").Log();
            return new CheckpointResponse
            {
                Success = false,
                Errors = new List<ApiError>
                {
                    new() { Code = 503, Message = "Service Unavailable: Maximum retry count exceeded" }
                },
                Version = ApiVersion.V1,
                IsTimeout = true
            };
        }

        // The polling budget can run out right after a non-JSON poll.
        if (response == null)
        {
            return Utilities.ParseCheckpointResponse(Utilities.RequestFailedBody)!;
        }

        if (Logger.IsTraceEnabled())
        {
            Logger.Trace($"Returning response: {Utilities.RedactSensitiveFields(response)}").Log();
        }

        return response;
    }

    // Static helper methods
    public static bool IsAllowed(CheckpointResponse response) => response.IsAllowed();
    public static bool IsDenied(CheckpointResponse response) => response.IsDenied();
    public static bool IsRunning(CheckpointResponse response) => response.IsRunning();
    public static bool IsUndecided(CheckpointResponse response) => response.IsUndecided();
    public static bool HasError(CheckpointResponse response) => response.HasError();
    public static bool IsTimeout(CheckpointResponse response) => response.IsTimedOut();

    private static void SkipEvent(string reason)
    {
        Logger.Error($"Composer event was not sent: {reason}").Log();
    }
}

/// <summary>
/// Backward compatibility wrapper (deprecated)
/// </summary>
[Obsolete("Use ComposerClient instead. This class is maintained for backward compatibility.")]
public class DodgeballClient : ComposerClient
{
    public DodgeballClient(string secretKey, ComposerConfig? config = null)
        : base(secretKey, config)
    {
    }
}
