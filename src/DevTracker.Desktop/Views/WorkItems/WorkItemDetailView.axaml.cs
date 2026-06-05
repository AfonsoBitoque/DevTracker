using Avalonia.Controls;
using Avalonia.Input.Platform;
using DevTracker.Desktop.ViewModels.WorkItems;

namespace DevTracker.Desktop.Views.WorkItems;

public partial class WorkItemDetailView : UserControl
{
    public WorkItemDetailView() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is WorkItemDetailViewModel vm)
        {
            vm.CopyToClipboardRequested += OnCopyToClipboardRequested;
        }
    }

    private async void OnCopyToClipboardRequested(string text)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is not null)
        {
            await topLevel.Clipboard.SetTextAsync(text);
        }
    }
}
