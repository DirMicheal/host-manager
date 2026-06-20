using System.Collections.ObjectModel;
using HostManage.Models;
using HostManage.Services;

namespace HostManage.ViewModels;

public class BackupListViewModel : ViewModelBase
{
    private readonly IBackupService _backupService;
    private readonly ILogService _logService;
    private readonly ISettingsService _settingsService;

    private BackupSnapshot? _selectedSnapshot;

    public ObservableCollection<BackupSnapshot> Snapshots => _backupService.Snapshots;

    public BackupSnapshot? SelectedSnapshot
    {
        get => _selectedSnapshot;
        set
        {
            SetProperty(ref _selectedSnapshot, value);
            RaiseCommands();
        }
    }

    public AsyncRelayCommand CreateCommand { get; }
    public AsyncRelayCommand RestoreCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand ExportCommand { get; }
    public AsyncRelayCommand ImportCommand { get; }
    public AsyncRelayCommand CleanCommand { get; }

    public BackupListViewModel(
        IBackupService backupService,
        ILogService logService,
        ISettingsService settingsService)
    {
        _backupService = backupService;
        _logService = logService;
        _settingsService = settingsService;

        CreateCommand = new AsyncRelayCommand(ExecuteCreateAsync);
        RestoreCommand = new AsyncRelayCommand(ExecuteRestoreAsync, () => SelectedSnapshot != null);
        DeleteCommand = new AsyncRelayCommand(ExecuteDeleteAsync, () => SelectedSnapshot != null);
        ExportCommand = new AsyncRelayCommand(ExecuteExportAsync, () => SelectedSnapshot != null);
        ImportCommand = new AsyncRelayCommand(ExecuteImportAsync);
        CleanCommand = new AsyncRelayCommand(ExecuteCleanAsync);
    }

    public override async Task LoadAsync()
    {
        _logService.LogAction("BackupList_Load", "开始加载备份列表");
        IsBusy = true;
        BusyMessage = "正在加载备份列表...";

        try
        {
            await Task.CompletedTask;
            _logService.LogAction("BackupList_Load", $"备份列表加载完成，共 {Snapshots.Count} 个备份");
        }
        catch (Exception ex)
        {
            _logService.LogError("BackupList_Load", "备份列表加载失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private void RaiseCommands()
    {
        RestoreCommand.RaiseCanExecuteChanged();
        DeleteCommand.RaiseCanExecuteChanged();
        ExportCommand.RaiseCanExecuteChanged();
    }

    private async Task ExecuteCreateAsync()
    {
        _logService.LogAction("Backup_Create", "准备创建手动备份");
        IsBusy = true;
        BusyMessage = "正在创建备份...";

        try
        {
            var snapshot = await _backupService.CreateBackupAsync(null, true);
            SelectedSnapshot = snapshot;
            _logService.LogAction("Backup_Create", $"备份创建成功: {snapshot.Name}, 大小: {FormatSize(snapshot.Size)}");
        }
        catch (Exception ex)
        {
            _logService.LogError("Backup_Create", "备份创建失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteRestoreAsync()
    {
        if (SelectedSnapshot == null) return;

        _logService.LogAction("Backup_Restore", $"准备恢复备份: {SelectedSnapshot.Name}");
        IsBusy = true;
        BusyMessage = "正在恢复备份...";

        try
        {
            await _backupService.CreateBackupAsync($"恢复前备份-{DateTime.Now:yyyyMMdd_HHmmss}", true);
            await _backupService.RestoreBackupAsync(SelectedSnapshot.Id);
            _logService.LogAction("Backup_Restore", $"备份恢复成功: {SelectedSnapshot.Name}");
        }
        catch (Exception ex)
        {
            _logService.LogError("Backup_Restore", "备份恢复失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteDeleteAsync()
    {
        if (SelectedSnapshot == null) return;

        _logService.LogAction("Backup_Delete", $"准备删除备份: {SelectedSnapshot.Name}");
        IsBusy = true;
        BusyMessage = "正在删除备份...";

        try
        {
            await _backupService.DeleteBackupAsync(SelectedSnapshot.Id);
            SelectedSnapshot = null;
            _logService.LogAction("Backup_Delete", "备份删除成功");
        }
        catch (Exception ex)
        {
            _logService.LogError("Backup_Delete", "备份删除失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteExportAsync()
    {
        if (SelectedSnapshot == null) return;

        _logService.LogAction("Backup_Export", $"准备导出备份: {SelectedSnapshot.Name}");
        IsBusy = true;
        BusyMessage = "正在导出备份...";

        try
        {
            var targetPath = $"backup_export_{SelectedSnapshot.Id:N}.bak";
            var savedPath = await _backupService.ExportBackupAsync(SelectedSnapshot.Id, targetPath);
            _logService.LogAction("Backup_Export", $"备份已导出到: {savedPath}");
        }
        catch (Exception ex)
        {
            _logService.LogError("Backup_Export", "备份导出失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteImportAsync()
    {
        _logService.LogAction("Backup_Import", "准备导入备份");
        IsBusy = true;
        BusyMessage = "正在导入备份...";

        try
        {
            var sourcePath = "backup_import.bak";
            await _backupService.ImportBackupAsync(sourcePath);
            _logService.LogAction("Backup_Import", "备份导入成功");
        }
        catch (Exception ex)
        {
            _logService.LogError("Backup_Import", "备份导入失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteCleanAsync()
    {
        var maxCount = _settingsService.Current.MaxBackupCount;
        _logService.LogAction("Backup_Clean", $"准备清理备份（保留最近 {maxCount} 个）");
        IsBusy = true;
        BusyMessage = "正在清理备份...";

        try
        {
            var count = await _backupService.CleanupOldBackupsAsync(maxCount);
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

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
