namespace Spreedly.ComposerSdk;

/// <summary>
/// API version constants
/// </summary>
public static class ApiVersion
{
    public const string V1 = "v1";
}

/// <summary>
/// Verification status constants
/// </summary>
public static class VerificationStatus
{
    public const string Pending = "PENDING";
    public const string Blocked = "BLOCKED";
    public const string Complete = "COMPLETE";
    public const string Failed = "FAILED";
}

/// <summary>
/// Verification outcome constants
/// </summary>
public static class VerificationOutcome
{
    public const string Approved = "APPROVED";
    public const string Denied = "DENIED";
    public const string Pending = "PENDING";
    public const string Error = "ERROR";
    public const string UsageLimit = "USAGE_LIMIT";
    public const string ApiLimit = "API_LIMIT";
}

/// <summary>
/// Configuration constants
/// </summary>
public static class Constants
{
    // Sent in the Composer-SDK-Version header. Keep in sync with <Version> in ComposerSdk.csproj.
    public const string SdkVersion = "1.0.0";
    public const string DefaultApiUrl = "https://api.dodgeballhq.com/";
    public const int DefaultRequestTimeoutMs = 30 * 1000; // 30 seconds
    public const int BaseCheckpointPollingWaitMs = 100;
    public const int MaxPollingWaitMs = 2000;
    public const int BaseCheckpointMaxTimeoutMs = 10 * 60 * 1000; // 10 minutes
    public const int MaxFailureCount = 3;
}
