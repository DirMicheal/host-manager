using System.Runtime.CompilerServices;
using HostManage.Models;

namespace HostManage.ViewModels;

public abstract class ViewModelBase : BindableBase
{
    private bool _isBusy;
    private string _busyMessage = string.Empty;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string BusyMessage
    {
        get => _busyMessage;
        set => SetProperty(ref _busyMessage, value);
    }

    protected new bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        return base.SetProperty(ref field, value, propertyName);
    }

    protected bool SetProperty<T>(ref T field, T value, IEqualityComparer<T> comparer, [CallerMemberName] string? propertyName = null)
    {
        if (comparer.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected bool SetProperty<T>(ref T field, T value, Action onChanged, [CallerMemberName] string? propertyName = null)
    {
        if (!base.SetProperty(ref field, value, propertyName))
            return false;

        onChanged?.Invoke();
        return true;
    }

    protected bool SetProperty<TModel, T>(T oldValue, T newValue, TModel model, Action<TModel, T> callback, [CallerMemberName] string? propertyName = null)
        where TModel : class
    {
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
            return false;

        callback?.Invoke(model, newValue);
        OnPropertyChanged(propertyName);
        return true;
    }

    public virtual Task LoadAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task UnloadAsync()
    {
        return Task.CompletedTask;
    }
}
