using Newtonsoft.Json;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Spreedly.ComposerSdk;

/// <summary>
/// Log levels for Composer SDK
/// </summary>
public enum ComposerLogLevel
{
    Trace,
    Info,
    Error,
    None
}

/// <summary>
/// Backward compatibility alias
/// </summary>
public enum DodgeballLogLevel
{
    Trace = ComposerLogLevel.Trace,
    Info = ComposerLogLevel.Info,
    Error = ComposerLogLevel.Error,
    None = ComposerLogLevel.None
}

/// <summary>
/// Logger severity for filtering
/// </summary>
public enum Severity
{
    Trace = 0,
    Info = 1,
    Error = 2,
    None = 3
}

/// <summary>
/// Structured log entry
/// </summary>
public class LogEntry
{
    public string Message { get; set; } = string.Empty;
    public Severity Severity { get; set; }
    public Exception? Error { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime Date { get; set; } = DateTime.UtcNow;

    public LogEntry(string message, Severity severity, Exception? error = null)
    {
        Message = message;
        Severity = severity;
        Error = error;
    }

    public LogEntry SetParameters(Dictionary<string, object>? parameters)
    {
        Parameters = parameters ?? new Dictionary<string, object>();
        return this;
    }

    public LogEntry SetParameter(string key, object value)
    {
        Parameters[key] = value;
        return this;
    }

    public LogEntry SetSeverity(Severity severity)
    {
        Severity = severity;
        return this;
    }

    public LogEntry SetError(Exception error)
    {
        Error = error;
        return this;
    }

    public void Log()
    {
        Logger.Log(this);
    }
}

/// <summary>
/// Logger class for Composer SDK
/// </summary>
public static class Logger
{
    public static Severity FilterLevel { get; set; } = Severity.Info;

    /// <summary>
    /// Reports whether a trace entry would be written. Callers check this before
    /// assembling an expensive trace message, because the message is built before
    /// Log gets a chance to discard it.
    /// </summary>
    public static bool IsTraceEnabled() => Severity.Trace >= FilterLevel;

    public static LogEntry Info(string message, Dictionary<string, object>? parameters = null)
    {
        var entry = new LogEntry(message, Severity.Info);
        if (parameters != null)
        {
            entry.SetParameters(parameters);
        }
        return entry;
    }

    public static LogEntry Trace(string message, Dictionary<string, object>? parameters = null)
    {
        var entry = new LogEntry(message, Severity.Trace);
        if (parameters != null)
        {
            entry.SetParameters(parameters);
        }
        return entry;
    }

    public static LogEntry Error(string message, Exception? error = null)
    {
        return new LogEntry(message, Severity.Error, error);
    }

    public static string FormatError(Exception? error)
    {
        if (error == null) return string.Empty;

        var errorData = new Dictionary<string, object>
        {
            ["message"] = error.Message,
            ["name"] = error.GetType().Name
        };

        if (error.StackTrace != null)
        {
            errorData["stack"] = error.StackTrace;
        }

        return JsonConvert.SerializeObject(errorData);
    }

    public static void Log(LogEntry logEntry)
    {
        if (logEntry.Severity < FilterLevel) return;

        var output = new Dictionary<string, object>
        {
            ["severity"] = SeverityToString(logEntry.Severity),
            ["date"] = logEntry.Date.ToString("o"),
            ["message"] = logEntry.Message
        };

        if (logEntry.Parameters.Count > 0)
        {
            output["parameters"] = logEntry.Parameters;
        }

        if (logEntry.Error != null)
        {
            output["error"] = FormatError(logEntry.Error);
        }

        Console.WriteLine(JsonConvert.SerializeObject(output));
    }

    private static string SeverityToString(Severity severity)
    {
        return severity switch
        {
            Severity.Trace => "TRACE",
            Severity.Info => "INFO",
            Severity.Error => "ERROR",
            Severity.None => "NONE",
            _ => "UNKNOWN"
        };
    }

    public static Severity LogLevelToSeverity(ComposerLogLevel logLevel)
    {
        return logLevel switch
        {
            ComposerLogLevel.Trace => Severity.Trace,
            ComposerLogLevel.Info => Severity.Info,
            ComposerLogLevel.Error => Severity.Error,
            ComposerLogLevel.None => Severity.None,
            _ => Severity.Info
        };
    }
}
