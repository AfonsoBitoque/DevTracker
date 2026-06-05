using System.Text.Json;
using DevTracker.Application.Abstractions.IO;
using DevTracker.Application.Abstractions.Settings;
using DevTracker.Application.Common;
using DevTracker.Application.Settings;

namespace DevTracker.Infrastructure.Settings;

/// <summary>
/// Persiste AppSettings em config.json (LocalApplicationData/DevTracker/).
/// Singleton. File corrompido ou inexistente → silenciar e usar defaults.
/// NUNCA guardar passwords, tokens ou dados sensíveis nas settings.
/// </summary>
public sealed class SettingsService(IAppPaths appPaths) : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented            = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy     = JsonNamingPolicy.CamelCase
    };

    private AppSettings _current = new();

    public AppSettings Current => _current;

    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(appPaths.ConfigPath))
            return _current = new AppSettings();

        try
        {
            await using var stream = File.OpenRead(appPaths.ConfigPath);
            _current = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, ct)
                       ?? new AppSettings();
        }
        catch
        {
            // File corrompido: ignorar e usar defaults
            _current = new AppSettings();
        }

        return _current;
    }

    public async Task<Result> SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(appPaths.AppDataRoot);
            await using var stream = File.Open(appPaths.ConfigPath, FileMode.Create, FileAccess.Write);
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, ct);
            _current = settings;
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Erro ao guardar configurações: {ex.Message}");
        }
    }

    public Task<bool> IsInitializedAsync(CancellationToken ct = default)
        => Task.FromResult(!string.IsNullOrWhiteSpace(_current.WorkspaceRoot)
                           && Directory.Exists(_current.WorkspaceRoot));
}
