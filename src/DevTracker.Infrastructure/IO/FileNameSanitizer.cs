using System.Text.RegularExpressions;

namespace DevTracker.Infrastructure.IO;

/// <summary>
/// Sanitiza nomes de pasta para uso seguro no filesystem.
/// Remove/substitui caracteres perigosos, evita path traversal e limita o comprimento.
/// </summary>
public static partial class FileNameSanitizer
{
    [GeneratedRegex("[^a-zA-Z0-9._ -]")]
    private static partial Regex InvalidCharsRegex();

    /// <summary>
    /// Sanitiza um nome de pasta: remove chars inválidos, substitui ".." por "-",
    /// colapsa espaços e limita a 80 caracteres.
    /// </summary>
    /// <exception cref="ArgumentException">Se o nome ficar vazio após sanitização.</exception>
    public static string SanitizeFolderName(string input)
    {
        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Name de pasta inválido ou vazio.");

        trimmed = trimmed.Replace("..", "-");
        trimmed = InvalidCharsRegex().Replace(trimmed, "-");
        trimmed = Regex.Replace(trimmed, @"\s+", " ");

        return trimmed.Length > 80 ? trimmed[..80].Trim() : trimmed;
    }
}
