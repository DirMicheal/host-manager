namespace HostManage.Models;

public class AppSettings : BindableBase
{
    private ThemeType _theme;
    private bool _autoBackup;
    private int _maxBackupCount = 30;
    private int _logRetentionDays = 90;
    private bool _autoStart;
    private bool _autoApplyEnvOnStartup;
    private Guid? _lastActiveEnvId;
    private bool _checkUpdate = true;
    private string _language = "zh-CN";
    private bool _showTutorialOnStartup = true;

    public ThemeType Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }

    public bool AutoBackup
    {
        get => _autoBackup;
        set => SetProperty(ref _autoBackup, value);
    }

    public int MaxBackupCount
    {
        get => _maxBackupCount;
        set => SetProperty(ref _maxBackupCount, value);
    }

    public int LogRetentionDays
    {
        get => _logRetentionDays;
        set => SetProperty(ref _logRetentionDays, value);
    }

    public bool AutoStart
    {
        get => _autoStart;
        set => SetProperty(ref _autoStart, value);
    }

    public bool AutoApplyEnvOnStartup
    {
        get => _autoApplyEnvOnStartup;
        set => SetProperty(ref _autoApplyEnvOnStartup, value);
    }

    public Guid? LastActiveEnvId
    {
        get => _lastActiveEnvId;
        set => SetProperty(ref _lastActiveEnvId, value);
    }

    public bool CheckUpdate
    {
        get => _checkUpdate;
        set => SetProperty(ref _checkUpdate, value);
    }

    public string Language
    {
        get => _language ?? string.Empty;
        set => SetProperty(ref _language, value ?? string.Empty);
    }

    public bool ShowTutorialOnStartup
    {
        get => _showTutorialOnStartup;
        set => SetProperty(ref _showTutorialOnStartup, value);
    }
}
