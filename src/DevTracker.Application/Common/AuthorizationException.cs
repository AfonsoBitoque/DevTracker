namespace DevTracker.Application.Common;

/// <summary>
/// Lançada exclusivamente por IPermissionService.EnsureCanPerformAsync quando o utilizador
/// não tem permissão para a operação solicitada.
/// ViewModels devem capturar esta exceção e apresentar mensagem de acesso negado.
/// </summary>
public sealed class AuthorizationException(string message) : Exception(message);
