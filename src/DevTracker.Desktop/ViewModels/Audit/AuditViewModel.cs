using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTracker.Application.Abstractions.Security;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;
using DevTracker.Application.DTOs.Audit;
using DevTracker.Application.Security;
using DevTracker.Desktop.ViewModels.Base;

namespace DevTracker.Desktop.ViewModels.Audit;

/// <summary>Página de auditoria — mostra as últimas 100 entradas de log.</summary>
public sealed partial class AuditViewModel(
    IAuditService              auditService,
    IPermissionSnapshotService permissionSnapshot) : PageViewModelBase
{
    [ObservableProperty] private ObservableCollection<AuditEntryDto> _entries = [];

    public override async Task OnActivatedAsync(CancellationToken ct = default)
    {
        var permissions = await permissionSnapshot.GetCurrentUserPermissionsAsync(ct);
        if (!permissions.Contains(Permissions.AuditRead))
        {
            SetError("No permission to view audit.");
            return;
        }
        await LoadAsync(ct);
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        IsBusy = true;
        ClearMessages();
        try
        {
            var result = await auditService.GetRecentAsync(100, ct);
            if (!result.Succeeded) { SetError(result.Error!); return; }
            Entries = new ObservableCollection<AuditEntryDto>(result.Value!);
        }
        catch (AuthorizationException) { SetError("No permission."); }
        catch (Exception ex)           { SetError(ex.Message); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync(CancellationToken.None);
}
