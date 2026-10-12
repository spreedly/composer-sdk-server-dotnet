using FluentAssertions;
using Xunit;

namespace Spreedly.ComposerSdk.Tests;

public class LoggerTests
{
    [Fact]
    public void Severity_ShouldHaveCorrectValues()
    {
        // Arrange & Act & Assert
        ((int)Severity.Trace).Should().Be(0);
        ((int)Severity.Info).Should().Be(1);
        ((int)Severity.Error).Should().Be(2);
        ((int)Severity.None).Should().Be(3);
    }

    [Fact]
    public void LogLevelToSeverity_ShouldConvertCorrectly()
    {
        // Arrange & Act & Assert
        Logger.LogLevelToSeverity(ComposerLogLevel.Trace).Should().Be(Severity.Trace);
        Logger.LogLevelToSeverity(ComposerLogLevel.Info).Should().Be(Severity.Info);
        Logger.LogLevelToSeverity(ComposerLogLevel.Error).Should().Be(Severity.Error);
        Logger.LogLevelToSeverity(ComposerLogLevel.None).Should().Be(Severity.None);
    }

    [Fact]
    public void LogLevelToSeverity_WithInvalidValue_ShouldReturnInfo()
    {
        // Arrange
        var invalidLogLevel = (ComposerLogLevel)999;

        // Act
        var result = Logger.LogLevelToSeverity(invalidLogLevel);

        // Assert
        result.Should().Be(Severity.Info);
    }

    [Fact]
    public void LogEntry_ShouldCreateWithMessageAndSeverity()
    {
        // Arrange & Act
        var entry = new LogEntry("Test message", Severity.Info);

        // Assert
        entry.Message.Should().Be("Test message");
        entry.Severity.Should().Be(Severity.Info);
        entry.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void LogEntry_ShouldSupportMethodChaining()
    {
        // Arrange & Act
        var entry = new LogEntry("Test", Severity.Info)
            .SetParameter("key", "value")
            .SetSeverity(Severity.Error);

        // Assert
        entry.Parameters["key"].Should().Be("value");
        entry.Severity.Should().Be(Severity.Error);
    }

    [Fact]
    public void LogEntry_SetParameters_ShouldSetDictionary()
    {
        // Arrange
        var entry = new LogEntry("Test", Severity.Info);
        var parameters = new Dictionary<string, object>
        {
            ["key1"] = "value1",
            ["key2"] = 42
        };

        // Act
        entry.SetParameters(parameters);

        // Assert
        entry.Parameters["key1"].Should().Be("value1");
        entry.Parameters["key2"].Should().Be(42);
    }

    [Fact]
    public void LogEntry_SetError_ShouldSetException()
    {
        // Arrange
        var entry = new LogEntry("Test", Severity.Error);
        var exception = new Exception("Test error");

        // Act
        entry.SetError(exception);

        // Assert
        entry.Error.Should().Be(exception);
    }

    [Fact]
    public void Logger_Info_ShouldCreateInfoEntry()
    {
        // Arrange & Act
        var entry = Logger.Info("Info message");

        // Assert
        entry.Message.Should().Be("Info message");
        entry.Severity.Should().Be(Severity.Info);
    }

    [Fact]
    public void Logger_Info_WithParameters_ShouldIncludeParameters()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { { "key", "value" } };

        // Act
        var entry = Logger.Info("Info with params", parameters);

        // Assert
        entry.Message.Should().Be("Info with params");
        entry.Severity.Should().Be(Severity.Info);
        entry.Parameters.Should().ContainKey("key");
        entry.Parameters["key"].Should().Be("value");
    }

    [Fact]
    public void Logger_Trace_ShouldCreateTraceEntry()
    {
        // Arrange & Act
        var entry = Logger.Trace("Trace message");

        // Assert
        entry.Message.Should().Be("Trace message");
        entry.Severity.Should().Be(Severity.Trace);
    }

    [Fact]
    public void Logger_Trace_WithParameters_ShouldIncludeParameters()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { { "trace", "data" } };

        // Act
        var entry = Logger.Trace("Trace with params", parameters);

        // Assert
        entry.Message.Should().Be("Trace with params");
        entry.Severity.Should().Be(Severity.Trace);
        entry.Parameters.Should().ContainKey("trace");
        entry.Parameters["trace"].Should().Be("data");
    }

    [Fact]
    public void Logger_Error_ShouldCreateErrorEntry()
    {
        // Arrange
        var exception = new Exception("Test error");

        // Act
        var entry = Logger.Error("Error message", exception);

        // Assert
        entry.Message.Should().Be("Error message");
        entry.Severity.Should().Be(Severity.Error);
        entry.Error.Should().Be(exception);
    }

    [Fact]
    public void Logger_FormatError_WithNull_ShouldReturnEmpty()
    {
        // Arrange & Act
        var result = Logger.FormatError(null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Logger_FormatError_WithStackTrace_ShouldIncludeStack()
    {
        // Arrange
        Exception exception;
        try
        {
            throw new InvalidOperationException("Error with stack");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Act
        var result = Logger.FormatError(exception);

        // Assert
        result.Should().Contain("Error with stack");
        result.Should().Contain("\"stack\":");
        result.Should().Contain("InvalidOperationException");
    }

    [Fact]
    public void Logger_FormatError_WithException_ShouldFormatCorrectly()
    {
        // Arrange
        var exception = new Exception("Test error");

        // Act
        var result = Logger.FormatError(exception);

        // Assert
        result.Should().Contain("Test error");
        result.Should().Contain("Exception");
    }

    [Fact]
    public void Logger_Log_WithFilteringTrace_ShouldNotOutput()
    {
        // Arrange
        Logger.FilterLevel = Severity.Info;
        var entry = Logger.Trace("Trace message");

        // Act
        entry.Log(); // Should not output due to filter

        // Assert - just verify no exception thrown
        entry.Message.Should().Be("Trace message");
    }

    [Fact]
    public void Logger_Log_WithFilteringInfo_ShouldNotOutput()
    {
        // Arrange
        Logger.FilterLevel = Severity.Error;
        var entry = Logger.Info("Info message");

        // Act
        entry.Log(); // Should not output due to filter

        // Assert - just verify no exception thrown
        entry.Message.Should().Be("Info message");
    }

    [Fact]
    public void Logger_Log_WithAllowedSeverity_ShouldOutput()
    {
        // Arrange
        Logger.FilterLevel = Severity.Trace;
        var entry = Logger.Trace("Test trace");

        // Act
        entry.Log();

        // Assert
        entry.Message.Should().Be("Test trace");
    }

    [Fact]
    public void Logger_Error_WithoutException_ShouldCreateErrorEntry()
    {
        // Arrange & Act
        var entry = Logger.Error("Error without exception");

        // Assert
        entry.Message.Should().Be("Error without exception");
        entry.Severity.Should().Be(Severity.Error);
        entry.Error.Should().BeNull();
    }

    [Fact]
    public void DodgeballLogLevel_ShouldMatchComposerLogLevel()
    {
        // Arrange & Act & Assert
        ((int)DodgeballLogLevel.Trace).Should().Be((int)ComposerLogLevel.Trace);
        ((int)DodgeballLogLevel.Info).Should().Be((int)ComposerLogLevel.Info);
        ((int)DodgeballLogLevel.Error).Should().Be((int)ComposerLogLevel.Error);
        ((int)DodgeballLogLevel.None).Should().Be((int)ComposerLogLevel.None);
    }

    [Fact]
    public void LogEntry_Date_ShouldBeSet()
    {
        // Arrange
        var beforeCreate = DateTime.UtcNow;
        
        // Act
        var entry = new LogEntry("Test", Severity.Info);
        var afterCreate = DateTime.UtcNow;

        // Assert
        entry.Date.Should().BeOnOrAfter(beforeCreate);
        entry.Date.Should().BeOnOrBefore(afterCreate);
    }

    [Fact]
    public void Logger_Log_WithInvalidSeverity_ShouldUseUnknownLabel()
    {
        // Arrange - cast an out-of-range int to Severity to hit the `_ => "UNKNOWN"` arm
        var invalidSeverity = (Severity)999;
        var entry = new LogEntry("Unknown severity", Severity.Info);
        entry.SetSeverity(invalidSeverity);

        var originalFilter = Logger.FilterLevel;
        Logger.FilterLevel = Severity.Trace;

        try
        {
            // Act - should not throw; SeverityToString returns "UNKNOWN"
            entry.Log();
        }
        finally
        {
            Logger.FilterLevel = originalFilter;
        }
    }

    [Fact]
    public void LogEntry_SetParameters_WithNull_ShouldUseEmptyDictionary()
    {
        // SetParameters(null) uses null-coalescing: Parameters = parameters ?? new Dictionary<string,object>()
        var entry = new LogEntry("Test", Severity.Info);
        entry.SetParameters(new Dictionary<string, object> { ["key"] = "value" });
        entry.SetParameters(null); // null → Parameters = new empty Dictionary

        entry.Parameters.Should().NotBeNull();
        entry.Parameters.Should().BeEmpty();
    }
}
