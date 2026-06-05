using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Desktop.ViewModels.Base;

namespace DevTracker.Desktop.ViewModels.WorkItems;

/// <summary>Diálogo para gerir e atribuir etiquetas a um Work Item.</summary>
public sealed partial class ManageLabelsDialogViewModel : DialogViewModelBase<IReadOnlyList<string>>
{
    [ObservableProperty] private string _newLabelName = string.Empty;

    public ObservableCollection<LabelSelectionItem> Labels { get; } = [];

    public Guid WorkItemId { get; set; }

    public void Initialize(IReadOnlyList<string> currentLabels, IReadOnlyList<string> availableLabels)
    {
        Labels.Clear();
        var currentSet = currentLabels.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var name in availableLabels.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n))
        {
            Labels.Add(new LabelSelectionItem(name, currentSet.Contains(name)));
        }
    }

    [RelayCommand]
    private void AddNewLabel()
    {
        var name = NewLabelName.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        if (Labels.Any(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            NewLabelName = string.Empty;
            return;
        }

        Labels.Add(new LabelSelectionItem(name, true));
        NewLabelName = string.Empty;
    }

    [RelayCommand]
    private void ConfirmDialog()
    {
        var selected = Labels.Where(l => l.IsSelected).Select(l => l.Name).ToList();
        ConfirmWithResult(selected);
    }

    [RelayCommand]
    private void CancelDialog() => Cancel();
}
