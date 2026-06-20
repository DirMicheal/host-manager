using System.Collections.ObjectModel;

namespace HostManage.Models;

public class HostRule : BindableBase
{
    private Guid _id;
    private string _ip = string.Empty;
    private string _domain = string.Empty;
    private string _comment = string.Empty;
    private bool _isEnabled;
    private RuleStatus _status;
    private DateTime _createdAt;
    private DateTime _updatedAt;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string IP
    {
        get => _ip ?? string.Empty;
        set => SetProperty(ref _ip, value ?? string.Empty);
    }

    public string Domain
    {
        get => _domain ?? string.Empty;
        set => SetProperty(ref _domain, value ?? string.Empty);
    }

    public string Comment
    {
        get => _comment ?? string.Empty;
        set => SetProperty(ref _comment, value ?? string.Empty);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public RuleStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
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

    public HostRule()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.Now;
        UpdatedAt = DateTime.Now;
        IsEnabled = true;
        Status = RuleStatus.Active;
    }
}
