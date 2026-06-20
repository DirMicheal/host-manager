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
        get => _action ?? string.Empty;
        set => SetProperty(ref _action, value ?? string.Empty);
    }

    public string Message
    {
        get => _message ?? string.Empty;
        set => SetProperty(ref _message, value ?? string.Empty);
    }

    public string UserName
    {
        get => _userName ?? string.Empty;
        set => SetProperty(ref _userName, value ?? string.Empty);
    }

    public string MachineName
    {
        get => _machineName ?? string.Empty;
        set => SetProperty(ref _machineName, value ?? string.Empty);
    }

    public string Details
    {
        get => _details ?? string.Empty;
        set => SetProperty(ref _details, value ?? string.Empty);
    }

    public OperationLog()
    {
        Id = Guid.NewGuid();
        Timestamp = DateTime.Now;
        Level = LogLevel.Info;
    }
}
