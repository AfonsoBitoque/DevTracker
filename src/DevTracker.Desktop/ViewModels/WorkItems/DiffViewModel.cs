using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Desktop.ViewModels.Base;

namespace DevTracker.Desktop.ViewModels.WorkItems;

public enum DiffLineType { Header, Context, Removed, Added }

public sealed record DiffLine(string Text, DiffLineType Type);

public sealed partial class DiffViewModel : DialogViewModelBase<DiffResult?>
{
    [ObservableProperty] private string _workItemTitle = string.Empty;
    [ObservableProperty] private string _workItemDescription = string.Empty;
    [ObservableProperty] private string _aiPrompt = string.Empty;
    [ObservableProperty] private int _actualPrompts;

    public ObservableCollection<DiffLine> LeftLines { get; } = [];
    public ObservableCollection<DiffLine> RightLines { get; } = [];

    public void SetDiff(string diff, string workItemTitle, string workItemDescription)
    {
        WorkItemTitle = workItemTitle;
        WorkItemDescription = workItemDescription ?? string.Empty;
        ParseDiff(diff);
    }

    private void ParseDiff(string rawDiff)
    {
        LeftLines.Clear();
        RightLines.Clear();

        if (string.IsNullOrWhiteSpace(rawDiff))
            return;

        var lines = rawDiff.Split(['\n'], StringSplitOptions.None);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Replace("\r", "");
            if (line.StartsWith("diff --git") || line.StartsWith("index ") ||
                line.StartsWith("--- ") || line.StartsWith("+++ ") ||
                line.StartsWith("@@"))
            {
                // Diff header / hunk header — show dimmed in both panes
                LeftLines.Add(new DiffLine(line, DiffLineType.Header));
                RightLines.Add(new DiffLine(line, DiffLineType.Header));
            }
            else if (line.StartsWith("-"))
            {
                // Removed — show in left (red), placeholder in right
                LeftLines.Add(new DiffLine(line[1..], DiffLineType.Removed));
                RightLines.Add(new DiffLine("", DiffLineType.Context));
            }
            else if (line.StartsWith("+"))
            {
                // Added — placeholder in left, show in right (green)
                LeftLines.Add(new DiffLine("", DiffLineType.Context));
                RightLines.Add(new DiffLine(line[1..], DiffLineType.Added));
            }
            else if (string.IsNullOrEmpty(line) || line.StartsWith(" ") || line.StartsWith("\\"))
            {
                // Context line (space prefix, empty line, or \" no newline)
                var text = line.StartsWith(" ") ? line[1..] : line;
                LeftLines.Add(new DiffLine(text, DiffLineType.Context));
                RightLines.Add(new DiffLine(text, DiffLineType.Context));
            }
            else
            {
                // Fallback — show as context in both
                LeftLines.Add(new DiffLine(line, DiffLineType.Context));
                RightLines.Add(new DiffLine(line, DiffLineType.Context));
            }
        }
    }

    [RelayCommand]
    private void Confirm() => ConfirmWithResult(new DiffResult(AiPrompt, ActualPrompts));

    [RelayCommand]
    private new void Cancel() => base.Cancel();
}
