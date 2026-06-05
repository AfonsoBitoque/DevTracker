namespace DevTracker.Application.Common;

/// <summary>
/// Resultado de uma operação sem valor de retorno.
/// Serviços retornam sempre Result/Result&lt;T&gt; — nunca lançam exceções para erros esperados.
/// A única exceção permitida é AuthorizationException, lançada por EnsureCanPerformAsync.
/// </summary>
public sealed record Result(bool Succeeded, string? Error = null)
{
    public static Result Success()           => new(true);
    public static Result Fail(string error)  => new(false, error);
}

/// <summary>Resultado de uma operação com valor de retorno tipado.</summary>
public sealed record Result<T>(bool Succeeded, T? Value = default, string? Error = null)
{
    public static Result<T> Success(T value) => new(true, value);
    public static Result<T> Fail(string error) => new(false, default, error);
}
