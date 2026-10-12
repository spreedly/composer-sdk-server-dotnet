using FluentAssertions;
using Xunit;
using System.Text;

namespace Spreedly.ComposerSdk.Tests;

/// <summary>
/// Tests that verify actual console output from the logger
/// </summary>
[Collection("Logger Output Tests")]
public class LoggerOutputTests : IDisposable
{
    private readonly StringWriter _consoleOutput;
    private readonly TextWriter _originalOutput;

    public LoggerOutputTests()
    {
        // Capture console output
        _originalOutput = Console.Out;
        _consoleOutput = new StringWriter();
        Console.SetOut(_consoleOutput);
    }

    public void Dispose()
    {
        // Restore original console output
        Console.SetOut(_originalOutput);
        _consoleOutput.Dispose();
        
        // Reset logger filter
        Logger.FilterLevel = Severity.None;
    }

    [Fact]
    public void Log_WithAllowedSeverity_ShouldOutputToConsole()
    {
        // Arrange
        _consoleOutput.GetStringBuilder().Clear(); // Clear any previous output
        Logger.FilterLevel = Severity.Trace;
        var entry = Logger.Info("Test log message");

        // Act
        entry.Log();

        // Assert
        var output = _consoleOutput.ToString();
        output.Should().Contain("Test log message");
        output.Should().Contain("\"severity\":\"INFO\"");
    }

    [Fact]
    public void Log_WithFilteredSeverity_ShouldNotOutputToConsole()
    {
        _consoleOutput.GetStringBuilder().Clear(); // Clear any previous output
        // Arrange
        Logger.FilterLevel = Severity.Error;
        var entry = Logger.Info("Filtered message");

        // Act
        entry.Log();

        // Assert
        var output = _consoleOutput.ToString();
        output.Should().BeEmpty();
    }

    [Fact]
    public void Log_WithParameters_ShouldIncludeParametersInOutput()
    {
        // Arrange
        _consoleOutput.GetStringBuilder().Clear(); // Clear any previous output
        Logger.FilterLevel = Severity.Trace;
        var entry = Logger.Info("Message with params")
            .SetParameter("userId", "user-123")
            .SetParameter("count", 42);

        // Act
        entry.Log();

        // Assert
        var output = _consoleOutput.ToString();
        output.Should().Contain("Message with params");
        output.Should().Contain("userId");
        output.Should().Contain("user-123");
        output.Should().Contain("count");
        output.Should().Contain("42");
    }

    [Fact]
    public void Log_WithException_ShouldIncludeErrorInOutput()
    {        _consoleOutput.GetStringBuilder().Clear(); // Clear any previous output        // Arrange
        Logger.FilterLevel = Severity.Trace;
        var exception = new InvalidOperationException("Test exception");
        var entry = Logger.Error("Error occurred", exception);

        // Act
        entry.Log();

        // Assert
        var output = _consoleOutput.ToString();
        output.Should().Contain("Error occurred");
        output.Should().Contain("Test exception");
        output.Should().Contain("\"severity\":\"ERROR\"");
    }

    [Fact]
    public void Log_OutputFormat_ShouldBeValidJson()
    {
        // Arrange
        Logger.FilterLevel = Severity.Trace;
        var entry = Logger.Trace("Trace message");

        // Act
        entry.Log();

        // Assert
        var output = _consoleOutput.ToString().Trim();
        output.Should().StartWith("{");
        output.Should().EndWith("}");
        output.Should().Contain("\"message\"");
        output.Should().Contain("\"severity\"");
        output.Should().Contain("\"date\"");
    }
    [Fact]
    public void Log_WithUnknownSeverity_ShouldHandleGracefully()
    {
        // Arrange
        Logger.FilterLevel = Severity.Trace;
        _consoleOutput.GetStringBuilder().Clear(); // Clear any previous output
        var entry = new LogEntry("Test", (Severity)999); // Invalid severity value

        // Act
        entry.Log();

        // Assert
        var output = _consoleOutput.ToString();
        output.Should().Contain("\"severity\":\"UNKNOWN\"");
    }}
