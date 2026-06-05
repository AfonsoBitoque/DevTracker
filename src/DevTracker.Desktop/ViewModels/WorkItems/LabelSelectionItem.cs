using CommunityToolkit.Mvvm.ComponentModel;

namespace DevTracker.Desktop.ViewModels.WorkItems;

/// <summary>Item seleccionável para etiquetas no diálogo de gestão.</summary>
public sealed partial class LabelSelectionItem : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool   _isSelected;

    public LabelSelectionItem(string name, bool isSelected = false)
    {
        Name       = name;
        IsSelected = isSelected;
    }
}
