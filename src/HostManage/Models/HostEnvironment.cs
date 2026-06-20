using System.Collections.ObjectModel;

namespace HostManage.Models;

public class HostEnvironment : BindableBase
{
    private Guid _id;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private bool _isActive;
    private Guid? _proxyId;
    private ObservableCollection<HostRule> _rules = new();
    private DateTime _createdAt;
    private DateTime _updatedAt;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name ?? string.Empty;
        set => SetProperty(ref _name, value ?? string.Empty);
    }

    public string Description
    {
        get => _description ?? string.Empty;
        set => SetProperty(ref _description, value ?? string.Empty);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public Guid? ProxyId
    {
        get => _proxyId;
        set => SetProperty(ref _proxyId, value);
    }

    public ObservableCollection<HostRule> Rules
    {
        get => _rules ??= new ObservableCollection<HostRule>();
        set => SetProperty(ref _rules, value ?? new ObservableCollection<HostRule>());
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    public DateTime UpdatedAt
    {
        get => _updatedAt;
        set => SetProperty(ref _updatedAt, value);
    }

    public HostEnvironment()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.Now;
        UpdatedAt = DateTime.Now;
    }
}
