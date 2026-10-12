using FluentAssertions;
using Xunit;

namespace Spreedly.ComposerSdk.Tests;

public class UtilitiesTests
{
    [Fact]
    public void IsNullOrEmpty_WithNull_ShouldReturnTrue()
    {
        // Arrange & Act & Assert
        Utilities.IsNullOrEmpty(null).Should().BeTrue();
    }

    [Fact]
    public void IsNullOrEmpty_WithEmpty_ShouldReturnTrue()
    {
        // Arrange & Act & Assert
        Utilities.IsNullOrEmpty("").Should().BeTrue();
    }

    [Fact]
    public void IsNullOrEmpty_WithValue_ShouldReturnFalse()
    {
        // Arrange & Act & Assert
        Utilities.IsNullOrEmpty("test").Should().BeFalse();
    }

    [Fact]
    public void ConstructApiUrl_ShouldAddTrailingSlash()
    {
        // Arrange & Act
        var url = Utilities.ConstructApiUrl("https://api.test.com", "v1");

        // Assert
        url.Should().Be("https://api.test.com/v1/");
    }

    [Fact]
    public void ConstructApiUrl_WithTrailingSlash_ShouldPreserve()
    {
        // Arrange & Act
        var url = Utilities.ConstructApiUrl("https://api.test.com/", "v1");

        // Assert
        url.Should().Be("https://api.test.com/v1/");
    }

    [Fact]
    public void ConstructApiHeaders_ShouldIncludeRequiredHeaders()
    {
        // Arrange & Act
        var headers = Utilities.ConstructApiHeaders("secret-key");

        // Assert
        headers["Dodgeball-Secret-Key"].Should().Be("secret-key");
        headers["Composer-SDK-Version"].Should().Be("1.0.0");
        headers["Composer-SDK-Source"].Should().Be("dotnet-sdk");
        headers["Content-Type"].Should().Be("application/json");
    }

    [Fact]
    public void ConstructApiHeaders_WithOptionalHeaders_ShouldIncludeThem()
    {
        // Arrange & Act
        var headers = Utilities.ConstructApiHeaders(
            "secret-key",
            "ver-123",
            "source-token",
            "customer-id",
            "session-id");

        // Assert
        headers["Dodgeball-Verification-Id"].Should().Be("ver-123");
        headers["Dodgeball-Source-Token"].Should().Be("source-token");
        headers["Dodgeball-Customer-Id"].Should().Be("customer-id");
        headers["Dodgeball-Session-Id"].Should().Be("session-id");
    }

    [Fact]
    public void ConstructApiHeaders_WithNullOptionals_ShouldExcludeThem()
    {
        // Arrange & Act
        var headers = Utilities.ConstructApiHeaders("secret-key", null, null, null, null);

        // Assert
        headers.Should().NotContainKey("Dodgeball-Verification-Id");
        headers.Should().NotContainKey("Dodgeball-Source-Token");
        headers.Should().NotContainKey("Dodgeball-Customer-Id");
        headers.Should().NotContainKey("Dodgeball-Session-Id");
    }

    [Fact]
    public void ParseCheckpointResponse_WithValidJson_ShouldParse()
    {
        // Arrange
        var json = @"{
            ""success"": true,
            ""version"": ""v1"",
            ""verification"": {
                ""id"": ""ver-123"",
                ""status"": ""COMPLETE"",
                ""outcome"": ""APPROVED""
            }
        }";

        // Act
        var response = Utilities.ParseCheckpointResponse(json);

        // Assert
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Verification!.Id.Should().Be("ver-123");
    }

    [Fact]
    public void ParseCheckpointResponse_WithInvalidJson_ShouldReturnNull()
    {
        // Arrange
        var json = "invalid json";

        // Act
        var response = Utilities.ParseCheckpointResponse(json);

        // Assert
        response.Should().BeNull();
    }

    [Fact]
    public async Task SleepAsync_ShouldDelayExecution()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var delayMs = 50;

        // Act
        await Utilities.SleepAsync(delayMs);
        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        elapsed.Should().BeGreaterOrEqualTo(delayMs - 10); // Allow some tolerance
    }

}
