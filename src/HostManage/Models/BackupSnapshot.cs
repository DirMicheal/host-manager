namespace HostManage.Models;

public class BackupSnapshot : BindableBase
{
    private Guid _id;
    private string _name = string.Empty;
    private DateTime _timestamp;
    private string _contentHash = string.Empty;
    private string _filePath = string.Empty;
    private long _size;
    private bool _isManual;
    private string _description = string.Empty;

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

    public DateTime Timestamp
    {
        get => _timestamp;
        set => SetProperty(ref _timestamp, value);
    }

    public string ContentHash
    {
        get => _contentHash;
        set => SetProperty(ref _contentHash, value);
    }

    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    public long Size
    {
        get => _size;
        set => SetProperty(ref _size, value);
    }

    public bool IsManual
    {
        get => _isManual;
        set => SetProperty(ref _isManual, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public BackupSnapshot()
    {
        Id = Guid.NewGuid();
        Timestamp = DateTime.Now;
    }
}
