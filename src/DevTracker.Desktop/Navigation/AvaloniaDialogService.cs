using Avalonia.Controls;
using DevTracker.Application.Abstractions.Navigation;
using DevTracker.Desktop.ViewModels.Base;
using Microsoft.Extensions.DependencyInjection;

namespace DevTracker.Desktop.Navigation;

/// <summary>
/// Implementação de IDialogService para Avalonia.
/// Usa TaskCompletionSource para awaitar o fecho do dialog.
/// mainWindowFactory é um Func para evitar capturar a janela antes de estar criada.
/// </summary>
public sealed class AvaloniaDialogService(
    IServiceProvider  serviceProvider,
    Func<Window>      mainWindowFactory) : IDialogService
{
    public async Task<bool> ShowDialogAsync<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : class
    {
        using var scope = serviceProvider.CreateScope();
        var vm          = scope.ServiceProvider.GetRequiredService<TViewModel>();
        configure?.Invoke(vm);

        if (vm is not DialogViewModelBase dialogVm)
            throw new InvalidOperationException($"{typeof(TViewModel).Name} não é um DialogViewModelBase.");

        var tcs    = new TaskCompletionSource<bool>();
        var window = new DialogWindow { DataContext = vm };
        dialogVm.CloseRequested += result => { tcs.TrySetResult(result); window.Close(); };
        window.Closed += (_, _) => tcs.TrySetResult(false);

        await window.ShowDialog(mainWindowFactory());
        return await tcs.Task;
    }

    public async Task<TResult?> ShowDialogAsync<TViewModel, TResult>(Action<TViewModel>? configure = null) where TViewModel : class
    {
        using var scope = serviceProvider.CreateScope();
        var vm          = scope.ServiceProvider.GetRequiredService<TViewModel>();
        configure?.Invoke(vm);

        if (vm is not DialogViewModelBase<TResult> dialogVm)
            throw new InvalidOperationException($"{typeof(TViewModel).Name} não é um DialogViewModelBase<{typeof(TResult).Name}>.");

        var tcs    = new TaskCompletionSource<TResult?>();
        var window = new DialogWindow { DataContext = vm };
        dialogVm.CloseWithResult += result => { tcs.TrySetResult(result); window.Close(); };
        window.Closed += (_, _) => tcs.TrySetResult(default);

        await window.ShowDialog(mainWindowFactory());
        return await tcs.Task;
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource<bool>();

        // Botões
        var confirmBtn = new Button
        {
            Content             = "Confirm",
            Background          = Avalonia.Media.Brushes.Transparent,
            Foreground          = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#DC2626")),
            BorderBrush         = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#DC2626")),
            BorderThickness     = new Avalonia.Thickness(1),
            CornerRadius        = new Avalonia.CornerRadius(6),
            Padding             = new Avalonia.Thickness(20, 8),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        var cancelBtn = new Button
        {
            Content             = "Cancel",
            Background          = Avalonia.Media.Brushes.Transparent,
            Foreground          = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#A0A4B0")),
            BorderBrush         = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2D3142")),
            BorderThickness     = new Avalonia.Thickness(1),
            CornerRadius        = new Avalonia.CornerRadius(6),
            Padding             = new Avalonia.Thickness(20, 8),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };

        var btnRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 12
        };
        btnRow.Children.Add(cancelBtn);
        btnRow.Children.Add(confirmBtn);

        var panel = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 16 };
        panel.Children.Add(new TextBlock
        {
            Text         = title,
            FontSize     = 16,
            FontWeight   = Avalonia.Media.FontWeight.SemiBold,
            Foreground   = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F5F6F8"))
        });
        panel.Children.Add(new TextBlock
        {
            Text        = message,
            FontSize    = 13,
            Foreground  = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#A0A4B0")),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
        panel.Children.Add(btnRow);

        var dialog = new DialogWindow
        {
            Title                 = title,
            Width                 = 420,
            SizeToContent         = Avalonia.Controls.SizeToContent.Height,
            CanResize             = false,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
            Background            = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#181B22")),
            Content               = panel
        };

        confirmBtn.Click += (_, _) => { tcs.TrySetResult(true);  dialog.Close(); };
        cancelBtn.Click  += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };
        dialog.Closed    += (_, _) => tcs.TrySetResult(false);

        await dialog.ShowDialog(mainWindowFactory());
        return await tcs.Task;
    }

    public async Task AlertAsync(string title, string message)
    {
        var okBtn = new Button
        {
            Content             = "OK",
            Background          = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2563EB")),
            Foreground          = Avalonia.Media.Brushes.White,
            CornerRadius        = new Avalonia.CornerRadius(6),
            Padding             = new Avalonia.Thickness(24, 8),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 16 };
        panel.Children.Add(new TextBlock
        {
            Text       = title,
            FontSize   = 16,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F5F6F8"))
        });
        panel.Children.Add(new TextBlock
        {
            Text         = message,
            FontSize     = 13,
            Foreground   = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#A0A4B0")),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
        panel.Children.Add(okBtn);

        var dialog = new DialogWindow
        {
            Title                 = title,
            Width                 = 380,
            SizeToContent         = Avalonia.Controls.SizeToContent.Height,
            CanResize             = false,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
            Background            = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#181B22")),
            Content               = panel
        };

        okBtn.Click   += (_, _) => dialog.Close();
        await dialog.ShowDialog(mainWindowFactory());
    }
}
