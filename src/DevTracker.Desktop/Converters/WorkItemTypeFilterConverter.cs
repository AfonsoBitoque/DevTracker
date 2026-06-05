using System.Globalization;
using Avalonia.Data.Converters;
using DevTracker.Core.Enums;

namespace DevTracker.Desktop.Converters;

/// <summary>
/// Converte WorkItemType? para string legível para filtros de UI.
/// null -> "Todos", WorkItemType -> nome do enum.
/// </summary>
public sealed class WorkItemTypeFilterConverter : IValueConverter
{
    public static WorkItemTypeFilterConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is WorkItemType type ? type.ToString() : "Todos";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string str && Enum.TryParse<WorkItemType>(str, out var type) ? type : null;
}
