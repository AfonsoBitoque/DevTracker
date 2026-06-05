using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.DTOs.Dashboard;
using DevTracker.Desktop.ViewModels.Base;

namespace DevTracker.Desktop.ViewModels;

public sealed partial class DashboardViewModel(
    IDashboardService dashboardService) : PageViewModelBase
{
    [ObservableProperty] private DashboardMetricsDto? _metrics;
    [ObservableProperty] private bool _isLoading;

    public override async Task OnActivatedAsync(CancellationToken ct = default)
    {
        await LoadAsync(ct);
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            var result = await dashboardService.GetUserMetricsAsync(ct);
            if (result.Succeeded)
            {
                Metrics = result.Value;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
