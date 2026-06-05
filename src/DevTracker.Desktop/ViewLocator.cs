using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DevTracker.Desktop.ViewModels.Base;

namespace DevTracker.Desktop;

/// <summary>
/// Resolve Views a partir de ViewModels substituindo "ViewModels" por "Views"
/// no namespace completo e removendo o sufixo "ViewModel".
/// Ex: DevTracker.Desktop.ViewModels.Auth.LoginViewModel
///   → DevTracker.Desktop.Views.Auth.LoginView
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null) return null;

        var vmTypeName  = data.GetType().FullName!;
        var viewTypeName = vmTypeName
            .Replace(".ViewModels.", ".Views.")
            .Replace("ViewModel", "View");

        var viewType = Type.GetType(viewTypeName);
        if (viewType is null)
            return new TextBlock { Text = $"View não encontrada: {viewTypeName}" };

        return (Control?)Activator.CreateInstance(viewType)
            ?? new TextBlock { Text = $"Falha ao instanciar: {viewTypeName}" };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
