using FluentAssertions;
using RichardSzalay.MockHttp;
using Xunit;
using Newtonsoft.Json;
using System.Net;

namespace Spreedly.ComposerSdk.Tests;

public class ComposerIntegrationTests : IDisposable
{
    private readonly string _secretKey = "test-secret-key";
    private readonly string _apiUrl = "https://api.test.com/";
    private MockHttpMessageHandler? _mockHttp;

    public void Dispose()
    {
        Logger.FilterLevel = Severity.None;
        _mockHttp?.Dispose();
        
        // Reset to default HttpClient
        Utilities.SetHttpClient(new HttpClient());
    }

    [Fact]
    public async Task EventAsync_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        _mockHttp = new MockHttpMessageHandler();
        var trackRequest = _mockHttp.When($"{_apiUrl}v1/track")
            .Respond(HttpStatusCode.OK, "application/json", JsonConvert.SerializeObject(new { success = true }));

        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        var trackEvent = new TrackEvent
        {
            Type = "USER_LOGIN",
            Data = new Dictionary<string, object> { ["userId"] = "test-user" }
        };

        // Act
        await client.EventAsync(trackEvent, sessionId: "session-123");

        // Assert
        _mockHttp.GetMatchCount(trackRequest).Should().Be(1);
    }

    [Fact]
    public async Task EventAsync_WithMissingEvent_ShouldSkipWithoutSendingRequest()
    {
        _mockHttp = new MockHttpMessageHandler();
        var trackRequest = _mockHttp.When(HttpMethod.Post, $"{_apiUrl}v1/track")
            .Respond(HttpStatusCode.OK);
        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        await client.EventAsync(null!, sessionId: "session-123");
        _mockHttp.GetMatchCount(trackRequest).Should().Be(0);
    }

    [Fact]
    public async Task EventAsync_WithMissingEventType_ShouldSkipWithoutSendingRequest()
    {
        _mockHttp = new MockHttpMessageHandler();
        var trackRequest = _mockHttp.When(HttpMethod.Post, $"{_apiUrl}v1/track")
            .Respond(HttpStatusCode.OK);
        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        var trackEvent = new TrackEvent { Data = new Dictionary<string, object>() };

        await client.EventAsync(trackEvent, sessionId: "session-123");
        _mockHttp.GetMatchCount(trackRequest).Should().Be(0);
    }

    [Fact]
    public async Task EventAsync_WithWhitespaceEventType_ShouldSkipWithoutSendingRequest()
    {
        _mockHttp = new MockHttpMessageHandler();
        var trackRequest = _mockHttp.When(HttpMethod.Post, $"{_apiUrl}v1/track")
            .Respond(HttpStatusCode.OK);
        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        var trackEvent = new TrackEvent { Type = "   ", Data = new Dictionary<string, object>() };

        await client.EventAsync(trackEvent, sessionId: "session-123");
        _mockHttp.GetMatchCount(trackRequest).Should().Be(0);
    }

    [Fact]
    public async Task EventAsync_WithMissingSessionIdentifier_ShouldSkipWithoutSendingRequest()
    {
        _mockHttp = new MockHttpMessageHandler();
        var trackRequest = _mockHttp.When(HttpMethod.Post, $"{_apiUrl}v1/track")
            .Respond(HttpStatusCode.OK);
        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        var trackEvent = new TrackEvent { Type = "TEST", Data = new Dictionary<string, object>() };

        await client.EventAsync(trackEvent);
        _mockHttp.GetMatchCount(trackRequest).Should().Be(0);
    }

    [Fact]
    public async Task EventAsync_WithDisabledSDK_ShouldNotSendRequest()
    {
        // Arrange
        _mockHttp = new MockHttpMessageHandler();
        var trackRequest = _mockHttp.When(HttpMethod.Post, $"{_apiUrl}v1/track")
            .Respond(HttpStatusCode.OK);

        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var config = new ComposerConfig { ApiUrl = _apiUrl, IsEnabled = false, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        var trackEvent = new TrackEvent { Type = "TEST", Data = new Dictionary<string, object>() };

        // Act
        await client.EventAsync(trackEvent, sessionId: "session-123");

        // Assert
        _mockHttp.GetMatchCount(trackRequest).Should().Be(0);
    }

    [Fact]
    public async Task EventAsync_WithDisabledSDKAndMissingEvent_ShouldSkip()
    {
        var config = new ComposerConfig { ApiUrl = _apiUrl, IsEnabled = false, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        await client.EventAsync(null!, sessionId: "session-123");
    }

    [Fact]
    public async Task EventAsync_WithDisabledSDKAndMissingEventType_ShouldSkip()
    {
        var config = new ComposerConfig { ApiUrl = _apiUrl, IsEnabled = false, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        var trackEvent = new TrackEvent { Data = new Dictionary<string, object>() };

        await client.EventAsync(trackEvent, sessionId: "session-123");
    }

    [Fact]
    public async Task NotifyAsync_ShouldCallEventAsync()
    {
        // Arrange
        _mockHttp = new MockHttpMessageHandler();
        var trackRequest = _mockHttp.When(HttpMethod.Post, $"{_apiUrl}v1/track")
            .Respond(HttpStatusCode.OK);

        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        var trackEvent = new TrackEvent { Type = "TEST", Data = new Dictionary<string, object>() };

        // Act
        await client.NotifyAsync(trackEvent, sessionId: "session-123");

        // Assert
        _mockHttp.GetMatchCount(trackRequest).Should().Be(1);
    }

    [Fact]
    public async Task TrackAsync_ShouldCallEventAsync()
    {
        // Arrange
        _mockHttp = new MockHttpMessageHandler();
        var trackRequest = _mockHttp.When(HttpMethod.Post, $"{_apiUrl}v1/track")
            .Respond(HttpStatusCode.OK);

        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        var trackEvent = new TrackEvent { Type = "TEST", Data = new Dictionary<string, object>() };

        // Act
        await client.TrackAsync(trackEvent, sessionId: "session-123");

        // Assert
        _mockHttp.GetMatchCount(trackRequest).Should().Be(1);
    }

    [Fact]
    public async Task CheckpointAsync_WithMissingCheckpointName_ShouldThrow()
    {
        // Arrange
        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);
        var ev = new CheckpointEvent { Ip = "127.0.0.1", Data = new Dictionary<string, object>() };

        // Act & Assert
        await Assert.ThrowsAsync<MissingParameterException>(async () =>
            await client.CheckpointAsync("", ev, sessionId: "session-123"));
    }

    [Fact]
    public async Task CheckpointAsync_WithMissingEvent_ShouldThrow()
    {
        // Arrange
        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        // Act & Assert
        await Assert.ThrowsAsync<MissingParameterException>(async () =>
            await client.CheckpointAsync("TEST", null!, sessionId: "session-123"));
    }

    [Fact]
    public async Task CheckpointAsync_WithoutSessionOrSource_ShouldThrow()
    {
        // Arrange
        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);
        var ev = new CheckpointEvent { Ip = "127.0.0.1", Data = new Dictionary<string, object>() };

        // Act & Assert
        await Assert.ThrowsAsync<MissingParameterException>(async () =>
            await client.CheckpointAsync("TEST", ev));
    }

    [Fact]
    public async Task CheckpointAsync_WithDisabledSDK_ShouldReturnApproved()
    {
        // Arrange
        var config = new ComposerConfig
        {
            ApiUrl = _apiUrl,
            IsEnabled = false,
            LogLevel = ComposerLogLevel.None
        };
        var client = new ComposerClient(_secretKey, config);
        var ev = new CheckpointEvent { Ip = "127.0.0.1", Data = new Dictionary<string, object>() };

        // Act
        var response = await client.CheckpointAsync("TEST", ev, sessionId: "session-123");

        // Assert
        response.Success.Should().BeTrue();
        response.Verification!.Id.Should().Be("COMPOSER_IS_DISABLED");
        response.Verification.Outcome.Should().Be(VerificationOutcome.Approved);
    }

    [Fact]
    public void StaticHelpers_IsAllowed_ShouldWork()
    {
        // Arrange
        var response = new CheckpointResponse
        {
            Success = true,
            Verification = new Verification
            {
                Id = "ver-1",
                Status = VerificationStatus.Complete,
                Outcome = VerificationOutcome.Approved
            }
        };

        // Act & Assert
        ComposerClient.IsAllowed(response).Should().BeTrue();
    }

    [Fact]
    public void StaticHelpers_IsDenied_ShouldWork()
    {
        // Arrange
        var response = new CheckpointResponse
        {
            Success = true,
            Verification = new Verification
            {
                Id = "ver-1",
                Status = VerificationStatus.Complete,
                Outcome = VerificationOutcome.Denied
            }
        };

        // Act & Assert
        ComposerClient.IsDenied(response).Should().BeTrue();
    }

    [Fact]
    public void StaticHelpers_IsRunning_ShouldWork()
    {
        // Arrange
        var response = new CheckpointResponse
        {
            Success = true,
            Verification = new Verification
            {
                Id = "ver-1",
                Status = VerificationStatus.Pending,
                Outcome = VerificationOutcome.Pending
            }
        };

        // Act & Assert
        ComposerClient.IsRunning(response).Should().BeTrue();
    }

    [Fact]
    public void StaticHelpers_HasError_ShouldWork()
    {
        // Arrange
        var response = new CheckpointResponse { Success = false };

        // Act & Assert
        ComposerClient.HasError(response).Should().BeTrue();
    }

    [Fact]
    public void StaticHelpers_IsTimeout_ShouldWork()
    {
        // Arrange
        var response = new CheckpointResponse { Success = true, IsTimeout = true };

        // Act & Assert
        ComposerClient.IsTimeout(response).Should().BeTrue();
    }

    [Fact]
    public void StaticHelpers_IsUndecided_ShouldWork()
    {
        // Arrange
        var response = new CheckpointResponse
        {
            Success = true,
            Verification = new Verification
            {
                Id = "ver-1",
                Status = VerificationStatus.Complete,
                Outcome = VerificationOutcome.Pending
            }
        };

        // Act & Assert
        ComposerClient.IsUndecided(response).Should().BeTrue();
    }

    [Fact]
    public async Task CheckpointAsync_WithCompleteResponse_ShouldNotPoll()
    {
        // Arrange
        _mockHttp = new MockHttpMessageHandler();

        var completeResponse = new CheckpointResponse
        {
            Success = true,
            Version = ApiVersion.V1,
            Verification = new Verification
            {
                Id = "ver-123",
                Status = VerificationStatus.Complete,
                Outcome = VerificationOutcome.Approved,
                StepData = new Dictionary<string, object>()
            }
        };

        var checkpointRequest = _mockHttp.When(HttpMethod.Post, $"{_apiUrl}v1/checkpoint")
            .Respond("application/json", JsonConvert.SerializeObject(completeResponse));

        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        var ev = new CheckpointEvent { Ip = "127.0.0.1", Data = new Dictionary<string, object>() };

        // Act
        var response = await client.CheckpointAsync("TEST", ev, sessionId: "session-123");

        // Assert
        response.IsAllowed().Should().BeTrue();
        _mockHttp.GetMatchCount(checkpointRequest).Should().Be(1);
    }

    [Fact]
    public async Task CheckpointAsync_WithEmptyIp_ShouldLeaveIpOutOfRequest()
    {
        _mockHttp = new MockHttpMessageHandler();

        var completeResponse = new CheckpointResponse
        {
            Success = true,
            Version = ApiVersion.V1,
            Verification = new Verification
            {
                Id = "ver-123",
                Status = VerificationStatus.Complete,
                Outcome = VerificationOutcome.Approved,
                StepData = new Dictionary<string, object>()
            }
        };

        // Only matches when the event has no ip
        var checkpointRequest = _mockHttp.When(HttpMethod.Post, $"{_apiUrl}v1/checkpoint")
            .With(request =>
            {
                var body = Newtonsoft.Json.Linq.JObject.Parse(request.Content!.ReadAsStringAsync().Result);
                return !((Newtonsoft.Json.Linq.JObject)body["event"]!).ContainsKey("ip");
            })
            .Respond("application/json", JsonConvert.SerializeObject(completeResponse));

        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        var response = await client.CheckpointAsync("TEST", new CheckpointEvent(), sessionId: "session-123");

        response.IsAllowed().Should().BeTrue();
        _mockHttp.GetMatchCount(checkpointRequest).Should().Be(1);
    }

    [Fact]
    public async Task CheckpointAsync_WithPendingThenComplete_ShouldPoll()
    {
        // Arrange
        _mockHttp = new MockHttpMessageHandler();

        var pendingResponse = new CheckpointResponse
        {
            Success = true,
            Version = ApiVersion.V1,
            Verification = new Verification
            {
                Id = "ver-pending",
                Status = VerificationStatus.Pending,
                Outcome = VerificationOutcome.Pending,
                StepData = new Dictionary<string, object>()
            }
        };

        var completeResponse = new CheckpointResponse
        {
            Success = true,
            Version = ApiVersion.V1,
            Verification = new Verification
            {
                Id = "ver-pending",
                Status = VerificationStatus.Complete,
                Outcome = VerificationOutcome.Approved,
                StepData = new Dictionary<string, object>()
            }
        };

        var checkpointRequest = _mockHttp.When(HttpMethod.Post, $"{_apiUrl}v1/checkpoint")
            .Respond("application/json", JsonConvert.SerializeObject(pendingResponse));

        var verificationRequest = _mockHttp.When(HttpMethod.Get, $"{_apiUrl}v1/verification/ver-pending")
            .Respond("application/json", JsonConvert.SerializeObject(completeResponse));

        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        var ev = new CheckpointEvent { Ip = "127.0.0.1", Data = new Dictionary<string, object>() };

        // Act
        var response = await client.CheckpointAsync("TEST", ev, sessionId: "session-123");

        // Assert
        response.IsAllowed().Should().BeTrue();
        _mockHttp.GetMatchCount(checkpointRequest).Should().Be(1);
        _mockHttp.GetMatchCount(verificationRequest).Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task CheckpointAsync_WithBlocked_ShouldNotPoll()
    {
        // Arrange
        _mockHttp = new MockHttpMessageHandler();

        var blockedResponse = new CheckpointResponse
        {
            Success = true,
            Version = ApiVersion.V1,
            Verification = new Verification
            {
                Id = "ver-blocked",
                Status = VerificationStatus.Blocked,
                Outcome = VerificationOutcome.Pending,
                StepData = new Dictionary<string, object>()
            }
        };

        var checkpointRequest = _mockHttp.When(HttpMethod.Post, $"{_apiUrl}v1/checkpoint")
            .Respond("application/json", JsonConvert.SerializeObject(blockedResponse));

        var verificationRequest = _mockHttp.When(HttpMethod.Get, $"{_apiUrl}v1/verification/ver-blocked")
            .Respond("application/json", JsonConvert.SerializeObject(blockedResponse));

        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        var ev = new CheckpointEvent { Ip = "127.0.0.1", Data = new Dictionary<string, object>() };

        // Act
        var response = await client.CheckpointAsync("TEST", ev, sessionId: "session-123");

        // Assert
        // BLOCKED waits on a client action (e.g. MFA), so the SDK must return the
        // initial response immediately without polling the verification endpoint.
        _mockHttp.GetMatchCount(checkpointRequest).Should().Be(1);
        _mockHttp.GetMatchCount(verificationRequest).Should().Be(0);
        response.Verification!.Status.Should().Be(VerificationStatus.Blocked);
        response.IsRunning().Should().BeTrue();
        response.IsDenied().Should().BeFalse();
    }

    [Fact]
    public async Task CheckpointAsync_WithBlocked_ShouldReturnNextSteps()
    {
        _mockHttp = new MockHttpMessageHandler();
        _mockHttp.When(HttpMethod.Post, $"{_apiUrl}v1/checkpoint")
            .Respond("application/json",
                "{\"success\":true,\"version\":\"v1\",\"verification\":{\"id\":\"ver-blocked\",\"status\":\"BLOCKED\",\"outcome\":\"PENDING\","
                + "\"stepData\":{\"display\":{\"title\":\"Verify it's you\"}},"
                + "\"nextSteps\":[{\"id\":\"step-1\",\"name\":\"MFA\",\"url\":\"https://example.com/mfa.js\",\"config\":{\"key\":\"value\"}}]}}");
        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        var response = await client.CheckpointAsync("TEST", new CheckpointEvent(), sessionId: "session-123");

        response.Verification!.NextSteps.Should().ContainSingle();
        response.Verification.NextSteps![0]["id"].Should().Be("step-1");

        // ASP.NET Core serializes responses with System.Text.Json, so nested values must survive it
        var json = System.Text.Json.JsonSerializer.Serialize(response.Verification);
        json.Should().Contain("\"config\":{\"key\":\"value\"}");
        json.Should().Contain("\"display\":{\"title\":\"Verify it\\u0027s you\"}");
    }

    [Fact]
    public async Task CheckpointAsync_WithEmptyStatusDuringPolling_ShouldIncrementFailures()
    {
        // Arrange
        _mockHttp = new MockHttpMessageHandler();

        var pendingResponse = new CheckpointResponse
        {
            Success = true,
            Version = ApiVersion.V1,
            Verification = new Verification
            {
                Id = "ver-fail",
                Status = VerificationStatus.Pending,
                Outcome = VerificationOutcome.Pending,
                StepData = new Dictionary<string, object>()
            }
        };

        var badResponse = new CheckpointResponse
        {
            Success = true,
            Version = ApiVersion.V1,
            Verification = new Verification
            {
                Id = "ver-fail",
                Status = "",
                Outcome = VerificationOutcome.Pending,
                StepData = new Dictionary<string, object>()
            }
        };

        _mockHttp.When(HttpMethod.Post, $"{_apiUrl}v1/checkpoint")
            .Respond("application/json", JsonConvert.SerializeObject(pendingResponse));

        _mockHttp.When(HttpMethod.Get, $"{_apiUrl}v1/verification/ver-fail")
            .Respond("application/json", JsonConvert.SerializeObject(badResponse));

        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        var ev = new CheckpointEvent { Ip = "127.0.0.1", Data = new Dictionary<string, object>() };

        // Act
        var response = await client.CheckpointAsync("TEST", ev, sessionId: "session-123");

        // Assert
        response.HasError().Should().BeTrue();
        response.IsTimedOut().Should().BeTrue();
    }

    [Fact]
    public async Task CheckpointAsync_WhenPollingBudgetExpires_ShouldReturnPendingWithoutTimeout()
    {
        // Arrange
        _mockHttp = new MockHttpMessageHandler();

        var pendingResponse = new CheckpointResponse
        {
            Success = true,
            Version = ApiVersion.V1,
            Verification = new Verification
            {
                Id = "ver-timeout",
                Status = VerificationStatus.Pending,
                Outcome = VerificationOutcome.Pending,
                StepData = new Dictionary<string, object>()
            }
        };

        _mockHttp.When(HttpMethod.Post, $"{_apiUrl}v1/checkpoint")
            .Respond("application/json", JsonConvert.SerializeObject(pendingResponse));

        _mockHttp.When(HttpMethod.Get, $"{_apiUrl}v1/verification/ver-timeout")
            .Respond("application/json", JsonConvert.SerializeObject(pendingResponse));

        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        var ev = new CheckpointEvent { Ip = "127.0.0.1", Data = new Dictionary<string, object>() };
        var options = new CheckpointResponseOptions { Timeout = 50 };

        // Act
        var response = await client.CheckpointAsync("TEST", ev, sessionId: "session-123", options: options);

        // Assert
        response.Success.Should().BeTrue();
        response.IsTimedOut().Should().BeFalse();
        response.IsRunning().Should().BeTrue();
    }

    [Fact]
    public async Task CheckpointAsync_WithFailedResponse_ShouldReturnError()
    {
        // Arrange
        _mockHttp = new MockHttpMessageHandler();

        var errorResponse = new CheckpointResponse
        {
            Success = false,
            Errors = new List<ApiError> { new ApiError { Code = 500, Message = "Internal error" } }
        };

        _mockHttp.When(HttpMethod.Post, $"{_apiUrl}v1/checkpoint")
            .Respond("application/json", JsonConvert.SerializeObject(errorResponse));

        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        var ev = new CheckpointEvent { Ip = "127.0.0.1", Data = new Dictionary<string, object>() };

        // Act
        var response = await client.CheckpointAsync("TEST", ev, sessionId: "session-123");

        // Assert
        response.HasError().Should().BeTrue();
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CheckpointAsync_WithNullResponse_ShouldReturnError()
    {
        // Arrange
        _mockHttp = new MockHttpMessageHandler();

        _mockHttp.When(HttpMethod.Post, $"{_apiUrl}v1/checkpoint")
            .Respond("application/json", "null");

        Utilities.SetHttpClient(new HttpClient(_mockHttp));

        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);

        var ev = new CheckpointEvent { Ip = "127.0.0.1", Data = new Dictionary<string, object>() };

        // Act
        var response = await client.CheckpointAsync("TEST", ev, sessionId: "session-123");

        // Assert
        response.Success.Should().BeFalse();
    }

    [Fact]
    public void DodgeballClient_ShouldInheritFromComposer()
    {
        // Arrange & Act
        #pragma warning disable CS0618 // Type is obsolete
        var client = new DodgeballClient(_secretKey, new ComposerConfig { LogLevel = ComposerLogLevel.None });
        #pragma warning restore CS0618

        // Assert
        client.Should().BeAssignableTo<ComposerClient>();
    }
}
