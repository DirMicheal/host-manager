using System.Windows.Input;

namespace HostManage.ViewModels;

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;
    private readonly WeakEventManager _weakEventManager = new();

    public event EventHandler? CanExecuteChanged
    {
        add => _weakEventManager.AddEventHandler(value);
        remove => _weakEventManager.RemoveEventHandler(value);
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute ?? (() => true);
    }

    public bool CanExecute(object? parameter)
    {
        return _canExecute();
    }

    public void Execute(object? parameter)
    {
        _execute();
    }

    public void RaiseCanExecuteChanged()
    {
        _weakEventManager.HandleEvent(this, EventArgs.Empty, nameof(CanExecuteChanged));
    }
}

public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool> _canExecute;
    private readonly WeakEventManager _weakEventManager = new();
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged
    {
        add => _weakEventManager.AddEventHandler(value);
        remove => _weakEventManager.RemoveEventHandler(value);
    }

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute ?? (() => true);
    }

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && _canExecute();
    }

    public async void Execute(object? parameter)
    {
        await ExecuteAsync(parameter);
    }

    public async Task ExecuteAsync(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();
            await _execute();
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

public class WeakEventManager
{
    private readonly List<WeakReference<EventHandler>> _handlers = new();

    public void AddEventHandler(EventHandler? handler)
    {
        if (handler != null)
        {
            _handlers.Add(new WeakReference<EventHandler>(handler));
        }
    }

    public void RemoveEventHandler(EventHandler? handler)
    {
        if (handler == null) return;

        for (int i = _handlers.Count - 1; i >= 0; i--)
        {
            if (_handlers[i].TryGetTarget(out var target) && target == handler)
            {
                _handlers.RemoveAt(i);
            }
            else if (!_handlers[i].TryGetTarget(out _))
            {
                _handlers.RemoveAt(i);
            }
        }
    }

    public void HandleEvent(object? sender, EventArgs args, string eventName)
    {
        for (int i = _handlers.Count - 1; i >= 0; i--)
        {
            if (_handlers[i].TryGetTarget(out var handler))
            {
                handler?.Invoke(sender, args);
            }
            else
            {
                _handlers.RemoveAt(i);
            }
        }
    }
}
