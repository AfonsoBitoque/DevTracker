using DevTracker.Application.DTOs.Projects;
using DevTracker.Application.Validators.Projects;
using DevTracker.Core.Enums;
using FluentAssertions;

namespace DevTracker.Tests.Application.Validators;

public sealed class ChangeProjectStateRequestValidatorTests
{
    private readonly ChangeProjectStateRequestValidator _validator = new();

    [Theory]
    [InlineData(ProjectState.Paused)]
    [InlineData(ProjectState.Cancelled)]
    public async Task Validate_PausedOrCancelledWithoutReason_ShouldFail(ProjectState state)
    {
        // Arrange
        var request = new ChangeProjectStateRequest(Guid.NewGuid(), state, null);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Reason");
    }

    [Theory]
    [InlineData(ProjectState.Paused)]
    [InlineData(ProjectState.Cancelled)]
    public async Task Validate_PausedOrCancelledWithReason_ShouldPass(ProjectState state)
    {
        // Arrange
        var request = new ChangeProjectStateRequest(Guid.NewGuid(), state, "Razão válida");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(ProjectState.NotStarted)]
    [InlineData(ProjectState.InProgress)]
    [InlineData(ProjectState.Completed)]
    public async Task Validate_OtherStatesWithoutReason_ShouldPass(ProjectState state)
    {
        // Arrange
        var request = new ChangeProjectStateRequest(Guid.NewGuid(), state, null);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
