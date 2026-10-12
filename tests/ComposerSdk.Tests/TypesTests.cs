using FluentAssertions;
using Xunit;

namespace Spreedly.ComposerSdk.Tests;

public class TypesTests
{
    [Fact]
    public void CheckpointEvent_ShouldCreate()
    {
        // Arrange & Act
        var ev = new CheckpointEvent
        {
            Ip = "127.0.0.1",
            Data = new Dictionary<string, object> { ["test"] = "data" }
        };

        // Assert
        ev.Ip.Should().Be("127.0.0.1");
        ev.Data["test"].Should().Be("data");
    }

    [Fact]
    public void TrackEvent_ShouldCreate()
    {
        // Arrange & Act
        var ev = new TrackEvent
        {
            Type = "TEST_EVENT",
            Data = new Dictionary<string, object> { ["key"] = "value" }
        };

        // Assert
        ev.Type.Should().Be("TEST_EVENT");
        ev.Data["key"].Should().Be("value");
    }

    [Fact]
    public void Verification_ShouldCreate()
    {
        // Arrange & Act
        var verification = new Verification
        {
            Id = "ver-123",
            Status = VerificationStatus.Complete,
            Outcome = VerificationOutcome.Approved,
            StepData = new Dictionary<string, object>()
        };

        // Assert
        verification.Id.Should().Be("ver-123");
        verification.Status.Should().Be(VerificationStatus.Complete);
        verification.Outcome.Should().Be(VerificationOutcome.Approved);
    }

    [Fact]
    public void CheckpointResponse_IsAllowed_WithApprovedComplete_ShouldReturnTrue()
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
        response.IsAllowed().Should().BeTrue();
    }

    [Fact]
    public void CheckpointResponse_IsAllowed_WithDenied_ShouldReturnFalse()
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
        response.IsAllowed().Should().BeFalse();
    }

    [Fact]
    public void CheckpointResponse_IsDenied_WithDeniedComplete_ShouldReturnTrue()
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
        response.IsDenied().Should().BeTrue();
    }

    [Fact]
    public void CheckpointResponse_IsDenied_WithDeniedPending_ShouldReturnTrue()
    {
        // Arrange
        var response = new CheckpointResponse
        {
            Success = true,
            Verification = new Verification
            {
                Id = "ver-1",
                Status = VerificationStatus.Pending,
                Outcome = VerificationOutcome.Denied
            }
        };

        // Act & Assert
        response.IsDenied().Should().BeTrue();
    }

    [Fact]
    public void CheckpointResponse_IsDenied_WithDeniedBlocked_ShouldReturnTrue()
    {
        // Arrange
        var response = new CheckpointResponse
        {
            Success = true,
            Verification = new Verification
            {
                Id = "ver-1",
                Status = VerificationStatus.Blocked,
                Outcome = VerificationOutcome.Denied
            }
        };

        // Act & Assert
        response.IsDenied().Should().BeTrue();
    }

    [Fact]
    public void CheckpointResponse_IsDenied_WithUnsuccessfulDeniedComplete_ShouldReturnFalse()
    {
        // Arrange
        var response = new CheckpointResponse
        {
            Success = false,
            Verification = new Verification
            {
                Id = "ver-1",
                Status = VerificationStatus.Complete,
                Outcome = VerificationOutcome.Denied
            }
        };

        // Act & Assert
        response.IsDenied().Should().BeFalse();
    }

    [Fact]
    public void CheckpointResponse_IsRunning_WithPending_ShouldReturnTrue()
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
        response.IsRunning().Should().BeTrue();
    }

    [Fact]
    public void CheckpointResponse_IsRunning_WithBlocked_ShouldReturnTrue()
    {
        // Arrange
        var response = new CheckpointResponse
        {
            Success = true,
            Verification = new Verification
            {
                Id = "ver-1",
                Status = VerificationStatus.Blocked,
                Outcome = VerificationOutcome.Pending
            }
        };

        // Act & Assert
        response.IsRunning().Should().BeTrue();
    }

    [Fact]
    public void CheckpointResponse_IsRunning_WithDeniedBlocked_ShouldReturnFalse()
    {
        // Arrange
        var response = new CheckpointResponse
        {
            Success = true,
            Verification = new Verification
            {
                Id = "ver-1",
                Status = VerificationStatus.Blocked,
                Outcome = VerificationOutcome.Denied
            }
        };

        // Act & Assert
        response.IsRunning().Should().BeFalse();
    }

    [Fact]
    public void CheckpointResponse_IsRunning_WithDeniedPending_ShouldReturnFalse()
    {
        // Arrange
        var response = new CheckpointResponse
        {
            Success = true,
            Verification = new Verification
            {
                Id = "ver-1",
                Status = VerificationStatus.Pending,
                Outcome = VerificationOutcome.Denied
            }
        };

        // Act & Assert
        response.IsRunning().Should().BeFalse();
    }

    [Fact]
    public void CheckpointResponse_IsRunning_WithApprovedBlocked_ShouldReturnFalse()
    {
        // Arrange
        var response = new CheckpointResponse
        {
            Success = true,
            Verification = new Verification
            {
                Id = "ver-1",
                Status = VerificationStatus.Blocked,
                Outcome = VerificationOutcome.Approved
            }
        };

        // Act & Assert
        response.IsRunning().Should().BeFalse();
    }

    [Fact]
    public void CheckpointResponse_IsRunning_WithComplete_ShouldReturnFalse()
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
        response.IsRunning().Should().BeFalse();
    }

    [Fact]
    public void CheckpointResponse_HasError_WithUnsuccessful_ShouldReturnTrue()
    {
        // Arrange
        var response = new CheckpointResponse { Success = false };

        // Act & Assert
        response.HasError().Should().BeTrue();
    }

    [Fact]
    public void CheckpointResponse_HasError_WithErrorOutcome_ShouldReturnTrue()
    {
        // Arrange
        var response = new CheckpointResponse
        {
            Success = true,
            Verification = new Verification
            {
                Id = "ver-1",
                Status = VerificationStatus.Failed,
                Outcome = VerificationOutcome.Error
            }
        };

        // Act & Assert
        response.HasError().Should().BeTrue();
    }

    [Fact]
    public void CheckpointResponse_IsTimedOut_WithTimeout_ShouldReturnTrue()
    {
        // Arrange
        var response = new CheckpointResponse
        {
            Success = true,
            IsTimeout = true
        };

        // Act & Assert
        response.IsTimedOut().Should().BeTrue();
    }

    [Fact]
    public void CheckpointResponse_IsUndecided_WithPendingOutcome_ShouldReturnTrue()
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
        response.IsUndecided().Should().BeTrue();
    }

    [Fact]
    public void ComposerException_ShouldCreate()
    {
        // Arrange & Act
        var ex = new ComposerException("Test error");

        // Assert
        ex.Message.Should().Be("Test error");
    }

    [Fact]
    public void ComposerException_WithInnerException_ShouldCreate()
    {
        // Arrange
        var inner = new InvalidOperationException("Inner error");

        // Act
        var ex = new ComposerException("Outer error", inner);

        // Assert
        ex.Message.Should().Be("Outer error");
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void MissingParameterException_ShouldCreate()
    {
        // Arrange & Act
        var ex = new MissingParameterException("testParam");

        // Assert
        ex.Message.Should().Contain("testParam");
    }

    [Fact]
    public void MissingConfigException_ShouldCreate()
    {
        // Arrange & Act
        var ex = new MissingConfigException("testConfig");

        // Assert
        ex.Message.Should().Contain("testConfig");
    }

    [Fact]
    public void InvalidConfigException_ShouldCreate()
    {
        // Arrange & Act
        var ex = new InvalidConfigException("testField", "testValue");

        // Assert
        ex.Message.Should().Contain("testField");
        ex.Message.Should().Contain("testValue");
    }

    [Fact]
    public void CheckpointResponseOptions_ShouldCreate()
    {
        // Arrange & Act
        var options = new CheckpointResponseOptions
        {
            Timeout = 5000,
            Sync = true
        };

        // Assert
        options.Timeout.Should().Be(5000);
        options.Sync.Should().BeTrue();
    }

    [Fact]
    public void CheckpointResponseOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new CheckpointResponseOptions();

        // Assert
        options.Timeout.Should().BeNull();
        options.Sync.Should().BeNull();
    }

    [Fact]
    public void ApiError_ShouldCreate()
    {
        // Arrange & Act
        var error = new ApiError
        {
            Code = 500,
            Message = "Internal error"
        };

        // Assert
        error.Code.Should().Be(500);
        error.Message.Should().Be("Internal error");
    }

    [Fact]
    public void VerificationStepData_ShouldCreate()
    {
        // Arrange & Act
        var stepData = new VerificationStepData
        {
            Id = "step-1",
            Name = "Test Step",
            Data = new Dictionary<string, object> { ["test"] = "value" }
        };

        // Assert
        stepData.Id.Should().Be("step-1");
        stepData.Name.Should().Be("Test Step");
        stepData.Data!["test"].Should().Be("value");
    }

    [Fact]
    public void TrackEvent_WithEventTime_ShouldSet()
    {
        // Arrange & Act
        var ev = new TrackEvent
        {
            Type = "TEST",
            EventTime = 1234567890,
            Data = new Dictionary<string, object>()
        };

        // Assert
        ev.EventTime.Should().Be(1234567890);
    }

    [Fact]
    public void ComposerConfig_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var config = new ComposerConfig();

        // Assert
        config.ApiVersion.Should().Be("v1");
        config.ApiUrl.Should().Be(Constants.DefaultApiUrl);
        config.LogLevel.Should().Be(ComposerLogLevel.Info);
        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void CheckpointResponse_IsAllowed_WithNullVerification_ShouldReturnFalse()
    {
        var response = new CheckpointResponse { Success = true, Verification = null };
        response.IsAllowed().Should().BeFalse();
    }

    [Fact]
    public void CheckpointResponse_IsDenied_WithNullVerification_ShouldReturnFalse()
    {
        var response = new CheckpointResponse { Success = true, Verification = null };
        response.IsDenied().Should().BeFalse();
    }

    [Fact]
    public void CheckpointResponse_IsUndecided_WithNullVerification_ShouldReturnFalse()
    {
        var response = new CheckpointResponse { Success = true, Verification = null };
        response.IsUndecided().Should().BeFalse();
    }
}
