using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Diagnostics.CodeAnalysis;

namespace Spreedly.ComposerSdk;

/// <summary>
/// Utility functions for the Composer SDK
/// </summary>
public static class Utilities
{
    // No client-wide timeout: HttpClient's 100 s default would cap RequestTimeoutMs, which each request enforces itself
    private static HttpClient _httpClient = new() { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>
    /// Fields that carry end-user information and are masked before anything is logged.
    /// nextSteps is included because a blocked checkpoint returns integration config
    /// (including provider client secrets) alongside the full client integration bundle.
    /// </summary>
    private static readonly HashSet<string> SensitiveFields = new(StringComparer.Ordinal)
    {
        "ip",
        "data",
        "stepData",
        "nextSteps"
    };

    private const string Redacted = "[REDACTED]";

    /// <summary>
    /// Render a payload as JSON with end-user fields masked, so verbose logs show the
    /// shape of a request or response without exposing PII
    /// </summary>
    public static string RedactSensitiveFields(object? payload)
    {
        if (payload == null)
        {
            return "null";
        }

        return MaskSensitiveFields(JToken.FromObject(payload)).ToString(Formatting.None);
    }

    /// <summary>
    /// Walk a JSON tree and replace every sensitive field
    /// </summary>
    private static JToken MaskSensitiveFields(JToken token)
    {
        if (token is JObject payloadObject)
        {
            var masked = new JObject();
            foreach (var property in payloadObject.Properties())
            {
                if (SensitiveFields.Contains(property.Name))
                {
                    masked[property.Name] = Redacted;
                }
                else
                {
                    masked[property.Name] = MaskSensitiveFields(property.Value);
                }
            }
            return masked;
        }

        if (token is JArray payloadArray)
        {
            var masked = new JArray();
            foreach (var entry in payloadArray)
            {
                masked.Add(MaskSensitiveFields(entry));
            }
            return masked;
        }

        return token;
    }

    /// <summary>
    /// Set the HTTP client (for testing purposes)
    /// </summary>
    public static void SetHttpClient(HttpClient client)
    {
        _httpClient = client;
    }

    /// <summary>
    /// Get the current HTTP client
    /// </summary>
    [ExcludeFromCodeCoverage]
    private static HttpClient HttpClient => _httpClient;

    /// <summary>
    /// Check if a value is null or empty
    /// </summary>
    public static bool IsNullOrEmpty(string? value)
    {
        return string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// Construct the full API URL
    /// </summary>
    public static string ConstructApiUrl(string baseUrl, string apiVersion)
    {
        if (!baseUrl.EndsWith("/"))
        {
            baseUrl += "/";
        }
        return $"{baseUrl}{apiVersion}/";
    }

    /// <summary>
    /// Construct API headers
    /// </summary>
    public static Dictionary<string, string> ConstructApiHeaders(
        string secretKey,
        string? verificationId = null,
        string? sourceToken = null,
        string? customerId = null,
        string? sessionId = null)
    {
        var headers = new Dictionary<string, string>
        {
            ["Dodgeball-Secret-Key"] = secretKey,
            ["Composer-SDK-Version"] = Constants.SdkVersion,
            ["Composer-SDK-Source"] = "dotnet-sdk",
            ["Content-Type"] = "application/json"
        };

        if (!IsNullOrEmpty(verificationId))
        {
            headers["Dodgeball-Verification-Id"] = verificationId!;
        }

        if (!IsNullOrEmpty(sourceToken))
        {
            headers["Dodgeball-Source-Token"] = sourceToken!;
        }

        if (!IsNullOrEmpty(customerId))
        {
            headers["Dodgeball-Customer-Id"] = customerId!;
        }

        if (!IsNullOrEmpty(sessionId))
        {
            headers["Dodgeball-Session-Id"] = sessionId!;
        }

        return headers;
    }

    internal const string RequestFailedBody =
        "{\"success\":false,\"errors\":[{\"code\":500,\"message\":\"Internal Server Error\"}]}";

    /// <summary>
    /// Make an HTTP request. A network error or timeout returns the standard failure response
    /// instead of throwing, so it is handled like any other failed response.
    /// </summary>
    public static async Task<HttpResponseMessage> MakeRequestAsync(
        string url,
        HttpMethod method,
        Dictionary<string, string> headers,
        object? body = null,
        int requestTimeoutMs = Constants.DefaultRequestTimeoutMs)
    {
        var request = new HttpRequestMessage(method, url);

        foreach (var header in headers)
        {
            // TryAddWithoutValidation sends CR/LF/NUL as-is on HTTP/1.1.
            if (header.Value.IndexOfAny(['\r', '\n', '\0']) >= 0)
            {
                Logger.Error("A request header has a character that can't be sent, such as a newline.").Log();
                return new RequestFailedResponse
                {
                    Content = new StringContent(RequestFailedBody, Encoding.UTF8, "application/json")
                };
            }

            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (body != null && method == HttpMethod.Post)
        {
            var jsonBody = JsonConvert.SerializeObject(body);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            if (Logger.IsTraceEnabled())
            {
                Logger.Trace($"Request body: {RedactSensitiveFields(body)}").Log();
            }
        }

        // Without this, 0 would cancel instantly and -1 would wait forever, since the default HttpClient has no Timeout of its own
        if (requestTimeoutMs <= 0)
        {
            requestTimeoutMs = Constants.DefaultRequestTimeoutMs;
        }

        // A per-request token rather than HttpClient.Timeout, because the HttpClient is shared
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(requestTimeoutMs));

        try
        {
            return await HttpClient.SendAsync(request, cts.Token);
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
        {
            Logger.Error("There was an error making a request.", e).Log();
            return new RequestFailedResponse
            {
                Content = new StringContent(RequestFailedBody, Encoding.UTF8, "application/json")
            };
        }
    }

    /// <summary>
    /// Stand-in 500 returned when a request never got an HTTP response, so callers can tell it apart from a real one.
    /// </summary>
    internal sealed class RequestFailedResponse : HttpResponseMessage
    {
        public RequestFailedResponse() : base(System.Net.HttpStatusCode.InternalServerError) { }
    }

    /// <summary>
    /// Parse checkpoint response
    /// </summary>
    public static CheckpointResponse? ParseCheckpointResponse(string json)
    {
        try
        {
            return JsonConvert.DeserializeObject<CheckpointResponse>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Sleep for milliseconds
    /// </summary>
    public static async Task SleepAsync(int milliseconds)
    {
        await Task.Delay(milliseconds);
    }
}
