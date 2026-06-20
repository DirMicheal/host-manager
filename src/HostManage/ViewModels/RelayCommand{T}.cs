using System.Windows.Input;

namespace HostManage.ViewModels;

public class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool> _canExecute;
    private readonly WeakEventManager _weakEventManager = new();

    public event EventHandler? CanExecuteChanged
    {
        add => _weakEventManager.AddEventHandler(value);
        remove => _weakEventManager.RemoveEventHandler(value);
    }

    public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute ?? (_ => true);
    }

    bool ICommand.CanExecute(object? parameter)
    {
        return parameter is T t && CanExecute(t);
    }

    public bool CanExecute(T parameter)
    {
        return _canExecute(parameter);
    }

    void ICommand.Execute(object? parameter)
    {
        if (parameter is T t)
        {
            Execute(t);
        }
    }

    public void Execute(T parameter)
    {
        _execute(parameter);
    }

    public void RaiseCanExecuteChanged()
    {
        _weakEventManager.HandleEvent(this, EventArgs.Empty, nameof(CanExecuteChanged));
    }
}

public class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T, Task> _execute;
    private readonly Func<T, bool> _canExecute;
    private readonly WeakEventManager _weakEventManager = new();
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged
    {
        add => _weakEventManager.AddEventHandler(value);
        remove => _weakEventManager.RemoveEventHandler(value);
    }

    public AsyncRelayCommand(Func<T, Task> execute, Func<T, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute ?? (_ => true);
    }

    bool ICommand.CanExecute(object? parameter)
    {
        return parameter is T t && !_isExecuting && CanExecute(t);
    }

    public bool CanExecute(T parameter)
    {
        return _canExecute(parameter);
    }

    async void ICommand.Execute(object? parameter)
    {
        if (parameter is T t)
        {
            await ExecuteAsync(t);
        }
    }

    public async Task ExecuteAsync(T parameter)
    {
        if (!((ICommand)this).CanExecute(parameter))
            return;

        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();
            await _execute(parameter);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        _weakEventManager.HandleEvent(this, EventArgs.Empty, nameof(CanExecuteChanged));
    }
}
