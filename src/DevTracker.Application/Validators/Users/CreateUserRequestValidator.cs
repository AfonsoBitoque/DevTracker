using DevTracker.Application.DTOs.Users;
using DevTracker.Core.Enums;
using FluentValidation;

namespace DevTracker.Application.Validators.Users;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .MinimumLength(3).WithMessage("O username deve ter pelo menos 3 caracteres.")
            .MaximumLength(64)
            .Matches(@"^[a-zA-Z0-9._-]+$").WithMessage("O username só pode conter letras, números, '.', '_' e '-'.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(12).WithMessage("A password deve ter pelo menos 12 caracteres.")
            .Matches(@"[A-Z]").WithMessage("A password deve conter pelo menos uma maiúscula.")
            .Matches(@"[a-z]").WithMessage("A password deve conter pelo menos uma minúscula.")
            .Matches(@"\d").WithMessage("A password deve conter pelo menos um número.")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("A password deve conter pelo menos um símbolo.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Passwords do not match.");

        // Owner só pode ser criado via SetupFirstOwnerAsync — não por este método
        RuleFor(x => x.Role)
            .NotEqual(UserRole.Owner).WithMessage("Não é permitido criar utilizadores com role Owner por este método.");
    }
}
