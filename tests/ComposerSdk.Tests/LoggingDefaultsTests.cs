using FluentAssertions;
using Newtonsoft.Json;
using RichardSzalay.MockHttp;
using System.Net;
using Xunit;

namespace Spreedly.ComposerSdk.Tests;

/// <summary>
/// Regression coverage for the safe logging default: an integrator who passes no
/// log level must never see request bodies, response bodies or event IPs in their logs.
/// </summary>
[Collection("Logger Output Tests")]
public class LoggingDefaultsTests : IDisposable
{
    private const string EventIp = "203.0.113.7";
    private const string EventEmail = "shopper@example.com";

    private readonly string _apiUrl = "https://api.test.com/";
    private readonly StringWriter _consoleOutput;
    private readonly TextWriter _originalOutput;
    private MockHttpMessageHandler? _mockHttp;

    public LoggingDefaultsTests()
    {
        _originalOutput = Console.Out;
        _consoleOutput = new StringWriter();
        Console.SetOut(_consoleOutput);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOutput);
        _consoleOutput.Dispose();

        Logger.FilterLevel = Severity.None;
        _mockHttp?.Dispose();
        Utilities.SetHttpClient(new HttpClient());
    }

    [Fact]
    public void DefaultConfig_ShouldUseInfoLogLevel()
    {
        new ComposerConfig().LogLevel.Should().Be(ComposerLogLevel.Info);
    }

    [Fact]
    public async Task DefaultLogLevel_ShouldEmitNothingForACheckpoint()
    {
        var output = await RunCheckpointAsync(null);

        Logger.FilterLevel.Should().Be(Severity.Info);
        output.Should().BeEmpty();
    }

    [Fact]
    public async Task TraceLogLevel_ShouldLogRedactedRequestAndResponsePayloads()
    {
        var output = await RunCheckpointAsync(ComposerLogLevel.Trace);

        output.Should().Contain("Request body: ");
        output.Should().Contain(VerificationStatus.Complete);
        output.Should().Contain("[REDACTED]");
        output.Should().NotContain(EventIp);
        output.Should().NotContain(EventEmail);
    }

    [Fact]
    public void RedactSensitiveFields_ShouldMaskNextSteps()
    {
        var payload = new Dictionary<string, object>
        {
            ["verification"] = new Dictionary<string, object>
            {
                ["id"] = "ver-123",
                ["status"] = "BLOCKED",
                ["nextSteps"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["name"] = "STRIPE_IDENTITY",
                        ["config"] = new Dictionary<string, object> { ["clientSecret"] = "vs_secret_value" },
                        ["content"] = new Dictionary<string, object> { ["text"] = "console.log(\"bundle\");" }
                    }
                }
            }
        };

        var redacted = Utilities.RedactSensitiveFields(payload);

        redacted.Should().Contain("BLOCKED");
        redacted.Should().Contain("[REDACTED]");
        redacted.Should().NotContain("vs_secret_value");
    }

    [Fact]
    public void IsTraceEnabled_ShouldTrackTheFilterLevel()
    {
        Logger.FilterLevel = Severity.Info;
        Logger.IsTraceEnabled().Should().BeFalse();

        Logger.FilterLevel = Severity.Trace;
        Logger.IsTraceEnabled().Should().BeTrue();
    }

    [Fact]
    public async Task CheckpointAsync_WithNonJsonResponse_ShouldReturnStandardFailureAndLogWithoutBody()
    {
        _mockHttp = new MockHttpMessageHandler();
        _mockHttp.When($"{_apiUrl}v1/checkpoint")
            .Respond(HttpStatusCode.BadGateway, "text/html", "<html>Bad Gateway</html>");
        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var client = new ComposerClient("test-secret-key", new ComposerConfig { ApiUrl = _apiUrl });

        var response = await client.CheckpointAsync("CHECKOUT", new CheckpointEvent { Ip = EventIp }, sessionId: "session-123");

        response.Success.Should().BeFalse();
        response.Errors![0].Code.Should().Be(500);
        _consoleOutput.ToString().Should().Contain("not valid JSON");
        _consoleOutput.ToString().Should().NotContain("Bad Gateway");
    }

    [Fact]
    public async Task CheckpointAsync_WithNonJsonPollResponses_ShouldCountTowardMaxFailures()
    {
        var pendingResponse = new CheckpointResponse
        {
            Success = true,
            Version = ApiVersion.V1,
            Verification = new Verification
            {
                Id = "ver-123",
                Status = VerificationStatus.Pending,
                Outcome = VerificationOutcome.Pending
            }
        };

        _mockHttp = new MockHttpMessageHandler();
        _mockHttp.When(HttpMethod.Post, $"{_apiUrl}v1/checkpoint")
            .Respond("application/json", JsonConvert.SerializeObject(pendingResponse));
        var polls = _mockHttp.When(HttpMethod.Get, $"{_apiUrl}v1/verification/ver-123")
            .Respond(HttpStatusCode.BadGateway, "text/html", "<html>Bad Gateway</html>");
        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var client = new ComposerClient("test-secret-key", new ComposerConfig { ApiUrl = _apiUrl });

        var response = await client.CheckpointAsync("CHECKOUT", new CheckpointEvent { Ip = EventIp }, sessionId: "session-123");

        response.Success.Should().BeFalse();
        response.Errors![0].Code.Should().Be(503);
        response.IsTimedOut().Should().BeTrue();
        _mockHttp.GetMatchCount(polls).Should().Be(Constants.MaxFailureCount);
        _consoleOutput.ToString().Should().Contain("not valid JSON");
        _consoleOutput.ToString().Should().NotContain("Bad Gateway");
    }

    [Fact]
    public async Task CheckpointAsync_WhenTimeoutExpiresAfterNonJsonPoll_ShouldReturnStandardFailure()
    {
        var pendingResponse = new CheckpointResponse
        {
            Success = true,
            Version = ApiVersion.V1,
            Verification = new Verification
            {
                Id = "ver-123",
                Status = VerificationStatus.Pending,
                Outcome = VerificationOutcome.Pending
            }
        };

        _mockHttp = new MockHttpMessageHandler();
        _mockHttp.When(HttpMethod.Post, $"{_apiUrl}v1/checkpoint")
            .Respond("application/json", JsonConvert.SerializeObject(pendingResponse));
        var polls = _mockHttp.When(HttpMethod.Get, $"{_apiUrl}v1/verification/ver-123")
            .Respond(HttpStatusCode.BadGateway, "text/html", "<html>Bad Gateway</html>");
        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var client = new ComposerClient("test-secret-key", new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None });

        var response = await client.CheckpointAsync("CHECKOUT", new CheckpointEvent { Ip = EventIp }, sessionId: "session-123",
            options: new CheckpointResponseOptions { Timeout = 1 });

        response.Should().NotBeNull();
        response.Success.Should().BeFalse();
        response.Errors![0].Code.Should().Be(500);
        _mockHttp.GetMatchCount(polls).Should().Be(1);
    }

    /// <summary>
    /// Runs a checkpoint against a stubbed API and returns everything the SDK logged.
    /// </summary>
    private async Task<string> RunCheckpointAsync(ComposerLogLevel? logLevel)
    {
        var stubbedResponse = new CheckpointResponse
        {
            Success = true,
            Errors = new List<ApiError>(),
            Version = ApiVersion.V1,
            Verification = new Verification
            {
                Id = "ver-123",
                Status = VerificationStatus.Complete,
                Outcome = VerificationOutcome.Approved,
                StepData = new Dictionary<string, object> { ["email"] = EventEmail }
            }
        };

        _mockHttp = new MockHttpMessageHandler();
        _mockHttp.When($"{_apiUrl}v1/checkpoint")
            .Respond(HttpStatusCode.OK, "application/json", JsonConvert.SerializeObject(stubbedResponse));
        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var config = new ComposerConfig { ApiUrl = _apiUrl };
        if (logLevel != null)
        {
            config.LogLevel = logLevel.Value;
        }

        var client = new ComposerClient("test-secret-key", config);
        var checkpointEvent = new CheckpointEvent
        {
            Ip = EventIp,
            Data = new Dictionary<string, object> { ["email"] = EventEmail }
        };

        _consoleOutput.GetStringBuilder().Clear();
        await client.CheckpointAsync("CHECKOUT", checkpointEvent, sessionId: "session-123");

        return _consoleOutput.ToString();
    }

    [Fact]
    public async Task EventAsync_WhenTheApiDoesNotAcceptTheEvent_ShouldLogTheStatus()
    {
        var output = await RunEventAsync(HttpStatusCode.BadRequest);

        output.Should().Contain("Composer event was not accepted by the API (HTTP 400).");
    }

    [Fact]
    public async Task EventAsync_WhenTheApiAcceptsTheEvent_ShouldLogNothing()
    {
        var output = await RunEventAsync(HttpStatusCode.Created);

        output.Should().BeEmpty();
    }

    [Fact]
    public async Task EventAsync_WhenTheRequestFails_ShouldNotReportAnApiRejection()
    {
        _mockHttp = new MockHttpMessageHandler();
        _mockHttp.When($"{_apiUrl}v1/track").Throw(new HttpRequestException("connection refused"));
        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var client = new ComposerClient("test-secret-key", new ComposerConfig { ApiUrl = _apiUrl });
        var trackEvent = new TrackEvent { Type = "TEST_EVENT", Data = new Dictionary<string, object>() };

        _consoleOutput.GetStringBuilder().Clear();
        await client.EventAsync(trackEvent, sessionId: "session-123");

        var output = _consoleOutput.ToString();
        output.Should().Contain("There was an error making a request.");
        output.Should().NotContain("not accepted by the API");
    }

    [Theory]
    [InlineData("sk_test_FAKE\n123")]
    [InlineData("sk_test_FAKE\r123")]
    [InlineData("abc\r\nX-Injected: yes")]
    [InlineData("\r\nX-Injected: yes")]
    [InlineData("user\0id")]
    public async Task MakeRequestAsync_WithCrLfOrNulInAHeader_ShouldReturn500WithoutSendingOrLeaking(string value)
    {
        _mockHttp = new MockHttpMessageHandler();
        var expected = _mockHttp.When("https://api.test.com/*").Respond("application/json", "{}");
        Utilities.SetHttpClient(new HttpClient(_mockHttp));
        Logger.FilterLevel = Severity.Error;
        _consoleOutput.GetStringBuilder().Clear();

        using var response = await Utilities.MakeRequestAsync(
            $"{_apiUrl}v1/checkpoint",
            HttpMethod.Post,
            new Dictionary<string, string> { ["Dodgeball-Secret-Key"] = value },
            new { key = "value" });

        var body = await response.Content.ReadAsStringAsync();
        var output = _consoleOutput.ToString();

        ((int)response.StatusCode).Should().Be(500);
        body.Should().Contain("Internal Server Error");
        output.Should().Contain("A request header has a character that can't be sent, such as a newline.");
        output.Should().NotContain("sk_test_FAKE");
        output.Should().NotContain("X-Injected");
        output.Should().NotContain("\0");
        _mockHttp.GetMatchCount(expected).Should().Be(0);
    }

    [Fact]
    public async Task CheckpointAsync_WithANewlineInTheSecretKey_ShouldReturn500WithoutSending()
    {
        _mockHttp = new MockHttpMessageHandler();
        var expected = _mockHttp.When($"{_apiUrl}v1/checkpoint").Respond("application/json", "{}");
        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var client = new ComposerClient("sk_test_FAKE\n123", new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None });
        var response = await client.CheckpointAsync("TEST", new CheckpointEvent { Ip = "127.0.0.1" }, sessionId: "session-123");

        response.Success.Should().BeFalse();
        response.Errors![0].Code.Should().Be(500);
        response.Errors[0].Message.Should().Be("Internal Server Error");
        _mockHttp.GetMatchCount(expected).Should().Be(0);
    }

    private async Task<string> RunEventAsync(HttpStatusCode status)
    {
        _mockHttp = new MockHttpMessageHandler();
        _mockHttp.When($"{_apiUrl}v1/track").Respond(status, "application/json", "{}");
        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var client = new ComposerClient("test-secret-key", new ComposerConfig { ApiUrl = _apiUrl });
        var trackEvent = new TrackEvent { Type = "TEST_EVENT", Data = new Dictionary<string, object>() };

        _consoleOutput.GetStringBuilder().Clear();
        await client.EventAsync(trackEvent, sessionId: "session-123");

        return _consoleOutput.ToString();
    }
}
