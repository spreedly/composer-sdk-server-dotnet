using Newtonsoft.Json;

namespace Spreedly.ComposerSdk;

/// <summary>
/// Configuration options for the Composer SDK
/// </summary>
public class ComposerConfig
{
    public string ApiVersion { get; set; } = "v1";
    public string ApiUrl { get; set; } = Constants.DefaultApiUrl;
    public ComposerLogLevel LogLevel { get; set; } = ComposerLogLevel.Info;
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// How long a single HTTP request may take, in milliseconds. Applies to every call the SDK makes.
    /// This is not the checkpoint polling budget.
    /// </summary>
    public int RequestTimeoutMs { get; set; } = Constants.DefaultRequestTimeoutMs;
}

/// <summary>
/// Checkpoint event data
/// </summary>
public class CheckpointEvent
{
    [JsonProperty("ip")]
    public string Ip { get; set; } = string.Empty;

    [JsonProperty("data")]
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Track event data
/// </summary>
public class TrackEvent
{
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("data")]
    public Dictionary<string, object> Data { get; set; } = new();

    [JsonProperty("eventTime")]
    public long? EventTime { get; set; }
}

/// <summary>
/// Verification step data
/// </summary>
public class VerificationStepData
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("data")]
    public Dictionary<string, object>? Data { get; set; }
}

/// <summary>
/// Verification details
/// </summary>
public class Verification
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("outcome")]
    public string Outcome { get; set; } = string.Empty;

    [JsonProperty("stepData")]
    [JsonConverter(typeof(PlainJsonConverter))]
    public Dictionary<string, object>? StepData { get; set; }

    /// <summary>
    /// Steps the Client SDK runs next, such as MFA
    /// </summary>
    [JsonProperty("nextSteps")]
    [JsonConverter(typeof(PlainJsonConverter))]
    public List<Dictionary<string, object?>>? NextSteps { get; set; }
}

/// <summary>
/// Reads stepData and nextSteps into plain dictionaries and lists instead of JObjects, so
/// System.Text.Json (ASP.NET Core's default) can pass them on to the Client SDK.
/// </summary>
internal class PlainJsonConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) => true;

    public override bool CanWrite => false;

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var plain = ToPlain(Newtonsoft.Json.Linq.JToken.Load(reader));

        if (objectType == typeof(List<Dictionary<string, object?>>))
        {
            return (plain as List<object?>)?.OfType<Dictionary<string, object?>>().ToList();
        }

        return plain as Dictionary<string, object?>;
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) =>
        throw new NotSupportedException();

    private static object? ToPlain(Newtonsoft.Json.Linq.JToken token) => token switch
    {
        Newtonsoft.Json.Linq.JObject obj => obj.Properties().ToDictionary(p => p.Name, p => ToPlain(p.Value)),
        Newtonsoft.Json.Linq.JArray array => array.Select(ToPlain).ToList(),
        Newtonsoft.Json.Linq.JValue value => value.Value,
        _ => null
    };
}

/// <summary>
/// API error details
/// </summary>
public class ApiError
{
    [JsonProperty("code")]
    public int? Code { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Checkpoint response options
/// </summary>
public class CheckpointResponseOptions
{
    [JsonProperty("sync")]
    public bool? Sync { get; set; }

    [JsonProperty("timeout")]
    public int? Timeout { get; set; }
}

/// <summary>
/// Checkpoint response
/// </summary>
public class CheckpointResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("errors")]
    public List<ApiError> Errors { get; set; } = new();

    [JsonProperty("version")]
    public string Version { get; set; } = ApiVersion.V1;

    [JsonProperty("verification")]
    public Verification? Verification { get; set; }

    [JsonProperty("isTimeout")]
    public bool IsTimeout { get; set; }

    /// <summary>
    /// Returns true if the checkpoint is approved
    /// </summary>
    public bool IsAllowed() =>
        Success &&
        Verification?.Status == VerificationStatus.Complete &&
        Verification?.Outcome == VerificationOutcome.Approved;

    /// <summary>
    /// Returns true if the checkpoint is denied
    /// </summary>
    public bool IsDenied() =>
        Success &&
        Verification?.Outcome == VerificationOutcome.Denied;

    /// <summary>
    /// Returns true if the checkpoint is still running
    /// </summary>
    public bool IsRunning() =>
        Success &&
        Verification != null &&
        (Verification.Status == VerificationStatus.Pending ||
         Verification.Status == VerificationStatus.Blocked) &&
        Verification.Outcome == VerificationOutcome.Pending;

    /// <summary>
    /// Returns true if the checkpoint outcome is undecided
    /// </summary>
    public bool IsUndecided() =>
        Success &&
        Verification?.Status == VerificationStatus.Complete &&
        Verification?.Outcome == VerificationOutcome.Pending;

    /// <summary>
    /// Returns true if there was an error
    /// </summary>
    public bool HasError() =>
        !Success || Verification?.Outcome == VerificationOutcome.Error;

    /// <summary>
    /// Returns true if the request timed out
    /// </summary>
    public bool IsTimedOut() => IsTimeout;
}

/// <summary>
/// Custom exceptions
/// </summary>
public class ComposerException : Exception
{
    public ComposerException(string message) : base(message) { }
    public ComposerException(string message, Exception inner) : base(message, inner) { }
}

public class MissingParameterException : ComposerException
{
    public MissingParameterException(string parameter) 
        : base($"Missing required parameter: {parameter}") { }
}

public class MissingConfigException : ComposerException
{
    public MissingConfigException(string config) 
        : base($"Missing required configuration: {config}") { }
}

public class InvalidConfigException : ComposerException
{
    public InvalidConfigException(string config, object value) 
        : base($"Invalid configuration value for {config}: {value}") { }
}
