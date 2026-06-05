namespace DevTracker.Application.Settings;

/// <summary>Modelo de configuração persistido em config.json. Nunca guardar segredos aqui.</summary>
public sealed class AppSettings
{
    public string        WorkspaceRoot             { get; set; } = string.Empty;
    public int           SessionIdleTimeoutMinutes { get; set; } = 15;
    public int           SessionMaxDurationHours   { get; set; } = 8;
    public int           SchemaVersion             { get; set; } = 1;
    public string        GitHubUsername            { get; set; } = string.Empty;
    public UiPreferences Ui                        { get; set; } = new();
}

public sealed class UiPreferences
{
    /// <summary>"Light", "Dark" ou "System".</summary>
    public string Theme        { get; set; } = "System";
    public string Language     { get; set; } = "pt-PT";
    public double SidebarWidth { get; set; } = 240;
}
