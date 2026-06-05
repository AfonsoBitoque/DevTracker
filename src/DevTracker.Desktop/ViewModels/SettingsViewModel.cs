using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Settings;
using DevTracker.Desktop.ViewModels.Base;

namespace DevTracker.Desktop.ViewModels;

public sealed partial class SettingsViewModel(
    ISettingsService settingsService,
    ISecretService   secretService) : PageViewModelBase
{
    [ObservableProperty] private string _gitHubToken = string.Empty;
    [ObservableProperty] private string _gitHubUsername = string.Empty;

    public override async Task OnActivatedAsync(CancellationToken ct = default)
    {
        await LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        var settings = await settingsService.LoadAsync();
        GitHubUsername = settings.GitHubUsername;

        var tokenResult = await secretService.GetSecretAsync("GitHubToken");
        if (tokenResult.Succeeded && !string.IsNullOrWhiteSpace(tokenResult.Value))
            GitHubToken = tokenResult.Value!;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        ClearMessages();
        try
        {
            var settings = settingsService.Current;
            settings.GitHubUsername = GitHubUsername;

            var result = await settingsService.SaveAsync(settings);
            if (!result.Succeeded) { SetError(result.Error!); return; }

            if (!string.IsNullOrWhiteSpace(GitHubToken))
            {
                var secretResult = await secretService.SaveSecretAsync("GitHubToken", GitHubToken);
                if (!secretResult.Succeeded) { SetError(secretResult.Error!); return; }
            }

            SetSuccess("Settings saved successfully.");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
