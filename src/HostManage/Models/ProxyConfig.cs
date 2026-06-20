namespace HostManage.Models;

public class ProxyConfig : BindableBase
{
    private Guid _id;
    private string _name = string.Empty;
    private ProxyType _type;
    private string _host = string.Empty;
    private int _port;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _isSystemProxy;
    private string _encryptedPassword = string.Empty;

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

    public ProxyType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    public string Host
    {
        get => _host ?? string.Empty;
        set => SetProperty(ref _host, value ?? string.Empty);
    }

    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public string Username
    {
        get => _username ?? string.Empty;
        set => SetProperty(ref _username, value ?? string.Empty);
    }

    public string Password
    {
        get => _password ?? string.Empty;
        set => SetProperty(ref _password, value ?? string.Empty);
    }

    public bool IsSystemProxy
    {
        get => _isSystemProxy;
        set => SetProperty(ref _isSystemProxy, value);
    }

    public string EncryptedPassword
    {
        get => _encryptedPassword ?? string.Empty;
        set => SetProperty(ref _encryptedPassword, value ?? string.Empty);
    }

    public ProxyConfig()
    {
        Id = Guid.NewGuid();
        Type = ProxyType.None;
    }
}
