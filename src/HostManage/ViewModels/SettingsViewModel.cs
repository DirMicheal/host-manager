using HostManage.Models;
using HostManage.Services;

namespace HostManage.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly ILogService _logService;
    private readonly IBackupService _backupService;

    private AppSettings _current = new();

    public AppSettings Current
    {
        get => _current;
        set => SetProperty(ref _current, value);
    }

    public List<ThemeType> ThemeOptions { get; } = new()
    {
        ThemeType.Light,
        ThemeType.Dark
    };

    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand ResetCommand { get; }
    public RelayCommand<ThemeType> ApplyThemeCommand { get; }
    public AsyncRelayCommand CleanLogsCommand { get; }
    public AsyncRelayCommand CleanBackupsCommand { get; }

    public SettingsViewModel(
        ISettingsService settingsService,
        IThemeService themeService,
        ILogService logService,
        IBackupService backupService)
    {
        _settingsService = settingsService;
        _themeService = themeService;
        _logService = logService;
        _backupService = backupService;

        SaveCommand = new AsyncRelayCommand(ExecuteSaveAsync);
        ResetCommand = new AsyncRelayCommand(ExecuteResetAsync);
        ApplyThemeCommand = new RelayCommand<ThemeType>(ExecuteApplyTheme);
        CleanLogsCommand = new AsyncRelayCommand(ExecuteCleanLogsAsync);
        CleanBackupsCommand = new AsyncRelayCommand(ExecuteCleanBackupsAsync);
    }

    public override async Task LoadAsync()
    {
        _logService.LogAction("Settings_Load", "开始加载设置");
        IsBusy = true;
        BusyMessage = "正在加载设置...";

        try
        {
            await _settingsService.LoadSettingsAsync();
            Current = _settingsService.Current;
            _logService.LogAction("Settings_Load", "设置加载完成");
        }
        catch (Exception ex)
        {
            _logService.LogError("Settings_Load", "设置加载失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteSaveAsync()
    {
        _logService.LogAction("Settings_Save", "准备保存设置");
        IsBusy = true;
        BusyMessage = "正在保存设置...";

        try
        {
            _themeService.ApplyTheme(Current.Theme);
            await _settingsService.SaveSettingsAsync();
            _logService.LogAction("Settings_Save", "设置保存成功");
        }
        catch (Exception ex)
        {
            _logService.LogError("Settings_Save", "设置保存失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteResetAsync()
    {
        _logService.LogAction("Settings_Reset", "准备重置设置");

        try
        {
            await _settingsService.LoadSettingsAsync();
            Current = _settingsService.Current;
            _themeService.ApplyTheme(Current.Theme);
            _logService.LogAction("Settings_Reset", "设置已重置为已保存的值");
        }
        catch (Exception ex)
        {
            _logService.LogError("Settings_Reset", "设置重置失败", ex.ToString());
        }
    }

    private void ExecuteApplyTheme(ThemeType theme)
    {
        _logService.LogAction("Theme_Apply", $"应用主题: {theme}");

        try
        {
            Current.Theme = theme;
            _themeService.ApplyTheme(theme);
            _logService.LogAction("Theme_Apply", $"主题已切换为: {theme}");
        }
        catch (Exception ex)
        {
            _logService.LogError("Theme_Apply", "主题应用失败", ex.ToString());
        }
    }

    private async Task ExecuteCleanLogsAsync()
    {
        _logService.LogAction("Logs_Clean", $"准备清理超过{Current.LogRetentionDays}天的日志");
        IsBusy = true;
        BusyMessage = "正在清理日志...";

        try
        {
            var count = await _logService.CleanupOldLogsAsync(Current.LogRetentionDays);
            _logService.LogAction("Logs_Clean", $"日志清理完成，共删除 {count} 条旧日志");
        }
        catch (Exception ex)
        {
            _logService.LogError("Logs_Clean", "日志清理失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteCleanBackupsAsync()
    {
        _logService.LogAction("Backup_Clean", $"准备清理超过{Current.MaxBackupCount}个的旧备份");
        IsBusy = true;
        BusyMessage = "正在清理备份...";

        try
        {
            var count = await _backupService.CleanupOldBackupsAsync(Current.MaxBackupCount);
            _logService.LogAction("Backup_Clean", $"备份清理完成，共删除 {count} 个旧备份");
        }
        catch (Exception ex)
        {
            _logService.LogError("Backup_Clean", "备份清理失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }
}
