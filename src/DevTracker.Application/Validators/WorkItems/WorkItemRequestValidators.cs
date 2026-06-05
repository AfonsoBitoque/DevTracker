using DevTracker.Application.DTOs.WorkItems;
using FluentValidation;

namespace DevTracker.Application.Validators.WorkItems;

public sealed class CreateWorkItemRequestValidator : AbstractValidator<CreateWorkItemRequest>
{
    public CreateWorkItemRequestValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(256).WithMessage("Title cannot exceed 256 characters.");
        RuleFor(x => x.Description)
            .MaximumLength(4096).When(x => x.Description is not null);
        RuleFor(x => x.DueDate)
            .GreaterThan(DateTime.UtcNow).WithMessage("Due date must be in the future.")
            .When(x => x.DueDate.HasValue);
    }
}

public sealed class AddCommentRequestValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentRequestValidator()
    {
        RuleFor(x => x.WorkItemId).NotEmpty();
        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("O comentário não pode estar vazio.")
            .MaximumLength(4096);
    }
}
