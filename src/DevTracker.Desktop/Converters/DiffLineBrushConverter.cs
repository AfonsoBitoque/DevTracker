using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DevTracker.Desktop.ViewModels.WorkItems;

namespace DevTracker.Desktop.Converters;

public sealed class DiffLineBrushConverter : IValueConverter
{
    public static DiffLineBrushConverter Instance { get; } = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DiffLineType type) return new SolidColorBrush(Colors.Transparent);

        var mode = parameter?.ToString() ?? "bg";
        return type switch
        {
            DiffLineType.Header  => mode == "bg"
                ? new SolidColorBrush(Color.Parse("#1E222D"))
                : new SolidColorBrush(Color.Parse("#6B7280")),
            DiffLineType.Context => mode == "bg"
                ? new SolidColorBrush(Color.Parse("#0D1117"))
                : new SolidColorBrush(Color.Parse("#D0D3DD")),
            DiffLineType.Removed => mode == "bg"
                ? new SolidColorBrush(Color.Parse("#3B1F1F"))
                : new SolidColorBrush(Color.Parse("#FCA5A5")),
            DiffLineType.Added   => mode == "bg"
                ? new SolidColorBrush(Color.Parse("#1F3B1F"))
                : new SolidColorBrush(Color.Parse("#86EFAC")),
            _ => new SolidColorBrush(Color.Parse("#0D1117"))
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
