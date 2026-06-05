using DevTracker.Application.Settings;
using FluentValidation;

namespace DevTracker.Application.Validators.Settings;

public sealed class AppSettingsValidator : AbstractValidator<AppSettings>
{
    public AppSettingsValidator()
    {
        RuleFor(x => x.WorkspaceRoot)
            .Must(Directory.Exists).WithMessage("O workspace root não existe no disco.")
            .When(x => !string.IsNullOrWhiteSpace(x.WorkspaceRoot));

        RuleFor(x => x.SessionIdleTimeoutMinutes)
            .InclusiveBetween(1, 480).WithMessage("Inactivity timeout must be between 1 and 480 minutes.");

        RuleFor(x => x.SessionMaxDurationHours)
            .InclusiveBetween(1, 24).WithMessage("A duração máxima da sessão deve estar entre 1 e 24 horas.");

        RuleFor(x => x.Ui.Theme)
            .Must(t => t is "Light" or "Dark" or "System")
            .WithMessage("Tema inválido. Use 'Light', 'Dark' ou 'System'.");
    }
}
