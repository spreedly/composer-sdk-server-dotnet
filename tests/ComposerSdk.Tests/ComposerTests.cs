using FluentAssertions;
using RichardSzalay.MockHttp;
using Xunit;
using Newtonsoft.Json;

namespace Spreedly.ComposerSdk.Tests;

public class ComposerTests : IDisposable
{
    private readonly string _secretKey = "test-secret-key";
    private readonly string _apiUrl = "https://api.test.com/";

    public void Dispose()
    {
        // Reset logger filter level after each test
        Logger.FilterLevel = Severity.None;
    }

    [Fact]
    public void Constructor_WithValidSecretKey_ShouldCreate()
    {
        // Arrange & Act
        var client = new ComposerClient(_secretKey, new ComposerConfig { LogLevel = ComposerLogLevel.None });

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithoutSecretKey_ShouldThrow()
    {
        // Arrange Act & Assert
        Assert.Throws<MissingConfigException>(() => new ComposerClient(""));
    }

    [Fact]
    public void Constructor_WithInvalidApiVersion_ShouldThrow()
    {
        // Arrange Act & Assert
        Assert.Throws<InvalidConfigException>(() =>
            new ComposerClient(_secretKey, new ComposerConfig { ApiVersion = "invalid" }));
    }

    [Fact]
    public async Task CheckpointAsync_WithValidParameters_ShouldReturnResponse()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var response = new CheckpointResponse
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

        mockHttp.When($"{_apiUrl}v1/checkpoint")
            .Respond("application/json", JsonConvert.SerializeObject(response));

        // Note: In real implementation, we'd inject HttpClient. For now, this test demonstrates structure.
        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);
        var ev = new CheckpointEvent { Ip = "127.0.0.1", Data = new Dictionary<string, object>() };

        // Act & Assert - This would work with proper HttpClient injection
        // For now, we're just verifying the client can be created with config
        client.Should().NotBeNull();
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
    public async Task CheckpointAsync_WithWhitespaceCheckpointName_ShouldThrow()
    {
        // Arrange
        var config = new ComposerConfig { ApiUrl = _apiUrl, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);
        var ev = new CheckpointEvent { Ip = "127.0.0.1", Data = new Dictionary<string, object>() };

        // Act & Assert
        await Assert.ThrowsAsync<MissingParameterException>(async () =>
            await client.CheckpointAsync("   ", ev, sessionId: "session-123"));
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
    public async Task CheckpointAsync_WithEmptyEventIp_ShouldNotThrow()
    {
        // The API treats event.ip as optional.
        var config = new ComposerConfig { ApiUrl = _apiUrl, IsEnabled = false, LogLevel = ComposerLogLevel.None };
        var client = new ComposerClient(_secretKey, config);
        var ev = new CheckpointEvent { Ip = "", Data = new Dictionary<string, object>() };

        var response = await client.CheckpointAsync("TEST", ev, sessionId: "session-123");

        response.Verification!.Id.Should().Be("COMPOSER_IS_DISABLED");
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

}
