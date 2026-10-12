using FluentAssertions;
using Xunit;

namespace Spreedly.ComposerSdk.Tests;

public class ConstantsTests
{
    [Fact]
    public void ApiVersion_V1_ShouldBeCorrect()
    {
        // Arrange & Act & Assert
        ApiVersion.V1.Should().Be("v1");
    }

    [Fact]
    public void VerificationStatus_ShouldHaveCorrectValues()
    {
        // Arrange & Act & Assert
        VerificationStatus.Pending.Should().Be("PENDING");
        VerificationStatus.Blocked.Should().Be("BLOCKED");
        VerificationStatus.Complete.Should().Be("COMPLETE");
        VerificationStatus.Failed.Should().Be("FAILED");
    }

    [Fact]
    public void VerificationOutcome_ShouldHaveCorrectValues()
    {
        // Arrange & Act & Assert
        VerificationOutcome.Approved.Should().Be("APPROVED");
        VerificationOutcome.Denied.Should().Be("DENIED");
        VerificationOutcome.Pending.Should().Be("PENDING");
        VerificationOutcome.Error.Should().Be("ERROR");
    }

    [Fact]
    public void Constants_DefaultApiUrl_ShouldBeCorrect()
    {
        // Arrange & Act & Assert
        Constants.DefaultApiUrl.Should().Be("https://api.dodgeballhq.com/");
    }

    [Fact]
    public void Constants_PollingValues_ShouldBeCorrect()
    {
        // Arrange & Act & Assert
        Constants.BaseCheckpointPollingWaitMs.Should().Be(100);
        Constants.MaxPollingWaitMs.Should().Be(2000);
        Constants.MaxFailureCount.Should().Be(3);
    }

    [Fact]
    public void Constants_TimeoutValues_ShouldBeCorrect()
    {
        // Arrange & Act & Assert
        Constants.BaseCheckpointMaxTimeoutMs.Should().Be(600000); // 10 minutes
    }
}
