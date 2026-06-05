using DevTracker.Application.DTOs.Projects;
using FluentValidation;

namespace DevTracker.Application.Validators.Projects;

public sealed class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MinimumLength(2).WithMessage("O nome deve ter pelo menos 2 caracteres.")
            .MaximumLength(128).WithMessage("Name cannot exceed 128 characters.")
            .Matches(@"^[^/\\:*?""<>|]+$").WithMessage("O nome contém caracteres inválidos.");

        RuleFor(x => x.Color)
            .NotEmpty()
            .Matches(@"^#[0-9A-Fa-f]{6}$").WithMessage("A cor deve ser um código hex válido (ex: #2563EB).");

        RuleFor(x => x.Description)
            .MaximumLength(1024).WithMessage("Description cannot exceed 1024 characters.")
            .When(x => x.Description is not null);
    }
}
