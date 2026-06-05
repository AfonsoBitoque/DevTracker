namespace DevTracker.Core.Enums;

/// <summary>
/// Estado atual de um projeto. Todas as transições são registadas em StateTransition.
/// Paused e Cancelled requerem Reason obrigatório (validado por FluentValidation).
/// </summary>
public enum ProjectState
{
    NotStarted = 0,
    InProgress = 1,
    Paused     = 2,
    Cancelled  = 3,
    Completed  = 4
}
