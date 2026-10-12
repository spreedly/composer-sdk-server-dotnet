using FluentAssertions;
using Newtonsoft.Json;
using RichardSzalay.MockHttp;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Spreedly.ComposerSdk.Tests;

/// <summary>
/// Coverage for the per-request HTTP timeout. In the logger collection because one test
/// captures console output.
/// </summary>
[Collection("Logger Output Tests")]
public class RequestTimeoutTests : IDisposable
{
    private readonly string _secretKey = "test-secret-key";
    private readonly string _apiUrl = "https://api.test.com/";
    private MockHttpMessageHandler? _mockHttp;

    public void Dispose()
    {
        Logger.FilterLevel = Severity.None;
        _mockHttp?.Dispose();
        Utilities.SetHttpClient(new HttpClient());
    }

    [Fact]
    public void DefaultConfig_ShouldUseA30SecondRequestTimeout()
    {
        new ComposerConfig().RequestTimeoutMs.Should().Be(30000);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveRequestTimeout_ShouldLogAndFallBackToDefault(int invalid)
    {
        var originalOutput = Console.Out;
        using var output = new StringWriter();
        Console.SetOut(output);
        try
        {
            new ComposerClient(_secretKey, new ComposerConfig { LogLevel = ComposerLogLevel.Error, RequestTimeoutMs = invalid });
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        output.ToString().Should().Contain(
            $"config.RequestTimeoutMs must be a positive integer. Ignoring {invalid} and using the 30000ms default.");
    }

    [Fact]
    public async Task CheckpointAsync_WhenUpstreamStalls_ShouldReturnStandardFailureWithinTheTimeout()
    {
        // Accepts connections but never responds
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _ = listener.AcceptTcpClientAsync();

        var config = new ComposerConfig
        {
            ApiUrl = $"http://127.0.0.1:{port}/",
            LogLevel = ComposerLogLevel.None,
            RequestTimeoutMs = 200
        };
        var client = new ComposerClient(_secretKey, config);
        var ev = new CheckpointEvent { Ip = "127.0.0.1", Data = new Dictionary<string, object>() };

        var stopwatch = Stopwatch.StartNew();
        var response = await client.CheckpointAsync("TEST", ev, sessionId: "session-123");
        stopwatch.Stop();

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000);
        response.Success.Should().BeFalse();
        response.Errors[0].Code.Should().Be(500);
        response.Errors[0].Message.Should().Be("Internal Server Error");
    }

    [Fact]
    public async Task MakeRequestAsync_WithNonPositiveTimeout_ShouldStillTimeOutAtTheDefault()
    {
        // Accepts connections but never responds
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _ = listener.AcceptTcpClientAsync();
        _ = listener.AcceptTcpClientAsync();

        var url = $"http://127.0.0.1:{port}/";
        var headers = new Dictionary<string, string>();

        // Both run at once so the test waits for the 30s default only once. Without the
        // fallback, 0 fails instantly and -1 never returns.
        var stopwatch = Stopwatch.StartNew();
        var responses = await Task.WhenAll(
                Utilities.MakeRequestAsync(url, HttpMethod.Get, headers, requestTimeoutMs: 0),
                Utilities.MakeRequestAsync(url, HttpMethod.Get, headers, requestTimeoutMs: -1))
            .WaitAsync(TimeSpan.FromSeconds(45));
        stopwatch.Stop();

        stopwatch.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(Constants.DefaultRequestTimeoutMs - 1000);
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.InternalServerError));
    }

    [Fact]
    public async Task CheckpointAsync_WhenPollsHitNetworkErrors_ShouldCountTowardMaxFailures()
    {
        _mockHttp = new MockHttpMessageHandler();
        var pendingResponse = new CheckpointResponse
        {
            Success = true,
            Version = ApiVersion.V1,
            Verification = new Verification
            {
                Id = "ver-net",
                Status = VerificationStatus.Pending,
                Outcome = VerificationOutcome.Pending,
                StepData = new Dictionary<string, object>()
            }
        };

        _mockHttp.When(HttpMethod.Post, $"{_apiUrl}v1/checkpoint")
            .Respond("application/json", JsonConvert.SerializeObject(pendingResponse));
        _mockHttp.When(HttpMethod.Get, $"{_apiUrl}v1/verification/ver-net")
            .Throw(new HttpRequestException("Connection reset"));

        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);
        var ev = new CheckpointEvent { Ip = "127.0.0.1", Data = new Dictionary<string, object>() };

        var response = await client.CheckpointAsync("TEST", ev, sessionId: "session-123");

        response.Success.Should().BeFalse();
        response.Errors[0].Code.Should().Be(503);
        response.IsTimedOut().Should().BeTrue();
    }
}
