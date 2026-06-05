using System.Security.Cryptography;
using System.Text.Json;
using DevTracker.Application.Abstractions.IO;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Common;
using Microsoft.AspNetCore.DataProtection;

namespace DevTracker.Infrastructure.Security;

/// <summary>
/// Implementação de ISecretService usando Microsoft.AspNetCore.DataProtection.
/// Persiste segredos encriptados num ficheiro JSON na AppDataRoot.
/// </summary>
public sealed class SecretService(IDataProtectionProvider protectionProvider, IAppPaths appPaths) : ISecretService
{
    private readonly IDataProtector _protector = protectionProvider.CreateProtector("DevTracker.Secrets.v1");
    private readonly string _secretsPath = Path.Combine(appPaths.AppDataRoot, "secrets.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<Result> SaveSecretAsync(string key, string value, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_secretsPath)!);

            var secrets = await LoadSecretsAsync(ct);
            secrets[key] = _protector.Protect(value);

            var json = JsonSerializer.Serialize(secrets, JsonOptions);
            await File.WriteAllTextAsync(_secretsPath, json, ct);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Erro ao guardar segredo: {ex.Message}");
        }
    }

    public async Task<Result<string?>> GetSecretAsync(string key, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(_secretsPath))
                return Result<string?>.Success(null);

            var secrets = await LoadSecretsAsync(ct);
            if (!secrets.TryGetValue(key, out var encryptedValue))
                return Result<string?>.Success(null);

            var decrypted = _protector.Unprotect(encryptedValue);
            return Result<string?>.Success(decrypted);
        }
        catch (CryptographicException)
        {
            return Result<string?>.Fail("Falha ao desencriptar o segredo. A chave de proteção pode ter sido alterada.");
        }
        catch (Exception ex)
        {
            return Result<string?>.Fail($"Erro ao ler segredo: {ex.Message}");
        }
    }

    private async Task<Dictionary<string, string>> LoadSecretsAsync(CancellationToken ct)
    {
        if (!File.Exists(_secretsPath))
            return new Dictionary<string, string>(StringComparer.Ordinal);

        try
        {
            var json = await File.ReadAllTextAsync(_secretsPath, ct);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
