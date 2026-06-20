namespace HostManage.Models;

public class OperationLog : BindableBase
{
    private Guid _id;
    private DateTime _timestamp;
    private LogLevel _level;
    private string _action = string.Empty;
    private string _message = string.Empty;
    private string _userName = string.Empty;
    private string _machineName = string.Empty;
    private string _details = string.Empty;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public DateTime Timestamp
    {
        get => _timestamp;
        set => SetProperty(ref _timestamp, value);
    }

    public LogLevel Level
    {
        get => _level;
        set => SetProperty(ref _level, value);
    }

    public string Action
    {
        get => _action;
        set => SetProperty(ref _action, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public string UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value);
    }

    public string MachineName
    {
        get => _machineName;
        set => SetProperty(ref _machineName, value);
    }

    public string Details
    {
        get => _details;
        set => SetProperty(ref _details, value);
    }

    public OperationLog()
    {
        Id = Guid.NewGuid();
        Timestamp = DateTime.Now;
        Level = LogLevel.Info;
    }
}
