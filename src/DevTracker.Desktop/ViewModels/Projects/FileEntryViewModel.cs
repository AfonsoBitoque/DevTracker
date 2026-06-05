namespace DevTracker.Desktop.ViewModels.Projects;

/// <summary>Representa uma entrada (ficheiro ou pasta) no filesystem do projeto.</summary>
public sealed class FileEntryViewModel(string name, string fullPath, bool isDirectory)
{
    public string Name        { get; } = name;
    public string FullPath    { get; } = fullPath;
    public bool   IsDirectory { get; } = isDirectory;
    public string Icon        => IsDirectory ? "📁" : GetFileIcon(name);

    private static string GetFileIcon(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".txt"  or ".md"   or ".rst"                    => "📄",
        ".html" or ".htm"  or ".css" or ".js" or ".ts"  => "🌐",
        ".cs"   or ".py"   or ".java" or ".go" or ".rs" => "📝",
        ".json" or ".yaml" or ".yml" or ".toml"         => "⚙️",
        ".png"  or ".jpg"  or ".jpeg" or ".gif" or ".svg" => "🖼️",
        ".zip"  or ".tar"  or ".gz"                     => "📦",
        ".pdf"                                           => "📕",
        _                                               => "📄"
    };
}
