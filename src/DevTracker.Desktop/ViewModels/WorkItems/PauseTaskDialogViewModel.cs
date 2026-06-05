using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Desktop.ViewModels.Base;

namespace DevTracker.Desktop.ViewModels.WorkItems;

public sealed partial class PauseTaskDialogViewModel : DialogViewModelBase<string?>
{
    [ObservableProperty] private string _note = string.Empty;

    [RelayCommand]
    private void Confirm() => ConfirmWithResult(Note);

    [RelayCommand]
    private new void Cancel() => base.Cancel();
}
