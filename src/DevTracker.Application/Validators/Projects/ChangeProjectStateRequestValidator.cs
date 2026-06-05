using DevTracker.Application.DTOs.Projects;
using DevTracker.Core.Enums;
using FluentValidation;

namespace DevTracker.Application.Validators.Projects;

public sealed class ChangeProjectStateRequestValidator : AbstractValidator<ChangeProjectStateRequest>
{
    /// <summary>Estados que obrigam a indicar uma razão para a transição.</summary>
    private static readonly HashSet<ProjectState> StatesRequiringReason = [ProjectState.Paused, ProjectState.Cancelled];

    public ChangeProjectStateRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required when pausing or cancelling a project.")
            .When(x => StatesRequiringReason.Contains(x.NewState));
    }
}
