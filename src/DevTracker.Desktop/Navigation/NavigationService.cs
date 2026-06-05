using DevTracker.Application.Abstractions.Navigation;
using DevTracker.Desktop.ViewModels.Base;
using Microsoft.Extensions.DependencyInjection;

namespace DevTracker.Desktop.Navigation;

public sealed class NavigationService(
    IServiceProvider          serviceProvider,
    Action<PageViewModelBase> setCurrentPage) : INavigationService
{
    private readonly Stack<(PageViewModelBase vm, IServiceScope scope)> _history = new();
    private readonly Stack<(PageViewModelBase vm, IServiceScope scope)> _forwardHistory = new();
    private PageViewModelBase? _current;
    private IServiceScope?     _currentScope;

    public bool CanNavigateBack  => _history.Count > 0;
    public bool CanNavigateForward => _forwardHistory.Count > 0;

    public event Action? StateChanged;

    public void NavigateTo<TViewModel>() where TViewModel : class
        => NavigateTo<TViewModel>(null);

    public void NavigateTo<TViewModel>(Action<TViewModel>? configure) where TViewModel : class
    {
        while (_forwardHistory.Count > 0)
        {
            var (_, forwardScope) = _forwardHistory.Pop();
            forwardScope.Dispose();
        }

        var scope = serviceProvider.CreateScope();
        var vm    = scope.ServiceProvider.GetRequiredService<TViewModel>();
        configure?.Invoke(vm);

        if (vm is not PageViewModelBase page)
        {
            scope.Dispose();
            throw new InvalidOperationException($"{typeof(TViewModel).Name} não é um PageViewModelBase.");
        }

        _ = _current?.OnDeactivatedAsync();
        if (_current is not null && _currentScope is not null)
            _history.Push((_current, _currentScope));

        _current      = page;
        _currentScope = scope;
        setCurrentPage(page);

        _ = page.OnActivatedAsync();
        StateChanged?.Invoke();
    }

    public void NavigateBack()
    {
        if (!CanNavigateBack) return;

        _ = _current?.OnDeactivatedAsync();

        if (_current is not null && _currentScope is not null)
            _forwardHistory.Push((_current, _currentScope));

        (_current, _currentScope) = _history.Pop();
        setCurrentPage(_current);
        _ = _current.OnActivatedAsync();
        StateChanged?.Invoke();
    }

    public void NavigateForward()
    {
        if (!CanNavigateForward) return;

        _ = _current?.OnDeactivatedAsync();

        if (_current is not null && _currentScope is not null)
            _history.Push((_current, _currentScope));

        (_current, _currentScope) = _forwardHistory.Pop();
        setCurrentPage(_current);
        _ = _current.OnActivatedAsync();
        StateChanged?.Invoke();
    }
}
