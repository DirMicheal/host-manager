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
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public ProxyType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }

    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public bool IsSystemProxy
    {
        get => _isSystemProxy;
        set => SetProperty(ref _isSystemProxy, value);
    }

    public string EncryptedPassword
    {
        get => _encryptedPassword;
        set => SetProperty(ref _encryptedPassword, value);
    }

    public ProxyConfig()
    {
        Id = Guid.NewGuid();
        Type = ProxyType.None;
    }
}
