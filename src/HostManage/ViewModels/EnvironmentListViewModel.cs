using System.Collections.ObjectModel;
using HostManage.Models;
using HostManage.Services;

namespace HostManage.ViewModels;

public class EnvironmentListViewModel : ViewModelBase
{
    private readonly IEnvironmentService _environmentService;
    private readonly ILogService _logService;
    private readonly IBackupService _backupService;

    private HostEnvironment? _selectedEnvironment;

    public ObservableCollection<HostEnvironment> Environments => _environmentService.Environments;

    public HostEnvironment? SelectedEnvironment
    {
        get => _selectedEnvironment;
        set
        {
            SetProperty(ref _selectedEnvironment, value);
            RaiseCommands();
        }
    }

    public AsyncRelayCommand CreateCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand SwitchCommand { get; }
    public AsyncRelayCommand<HostEnvironment> CopyCommand { get; }
    public AsyncRelayCommand EditCommand { get; }
    public AsyncRelayCommand CompareCommand { get; }

    public EnvironmentListViewModel(
        IEnvironmentService environmentService,
        ILogService logService,
        IBackupService backupService)
    {
        _environmentService = environmentService;
        _logService = logService;
        _backupService = backupService;

        CreateCommand = new AsyncRelayCommand(ExecuteCreateAsync);
        DeleteCommand = new AsyncRelayCommand(ExecuteDeleteAsync, () => SelectedEnvironment != null);
        SwitchCommand = new AsyncRelayCommand(ExecuteSwitchAsync, () => SelectedEnvironment != null && !SelectedEnvironment.IsActive);
        CopyCommand = new AsyncRelayCommand<HostEnvironment>(ExecuteCopyAsync, env => env != null);
        EditCommand = new AsyncRelayCommand(ExecuteEditAsync, () => SelectedEnvironment != null);
        CompareCommand = new AsyncRelayCommand(ExecuteCompareAsync, () => SelectedEnvironment != null);
    }

    public override async Task LoadAsync()
    {
        _logService.LogAction("EnvList_Load", "开始加载环境列表");
        IsBusy = true;
        BusyMessage = "正在加载环境列表...";

        try
        {
            await _environmentService.LoadEnvironmentsAsync();
            _logService.LogAction("EnvList_Load", $"环境列表加载完成，共 {Environments.Count} 个环境");
        }
        catch (Exception ex)
        {
            _logService.LogError("EnvList_Load", "环境列表加载失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private void RaiseCommands()
    {
        DeleteCommand.RaiseCanExecuteChanged();
        SwitchCommand.RaiseCanExecuteChanged();
        EditCommand.RaiseCanExecuteChanged();
        CompareCommand.RaiseCanExecuteChanged();
    }

    private Task ExecuteCreateAsync()
    {
        _logService.LogAction("Env_Create", "准备创建新环境");
        return Task.CompletedTask;
    }

    private async Task ExecuteDeleteAsync()
    {
        if (SelectedEnvironment == null) return;

        _logService.LogAction("Env_Delete", $"准备删除环境: {SelectedEnvironment.Name}");
        IsBusy = true;
        BusyMessage = "正在删除环境...";

        try
        {
            await _backupService.CreateBackupAsync($"删除前备份-{SelectedEnvironment.Name}", true);
            await _environmentService.DeleteEnvironmentAsync(SelectedEnvironment.Id);
            SelectedEnvironment = null;
            _logService.LogAction("Env_Delete", "环境删除成功");
        }
        catch (Exception ex)
        {
            _logService.LogError("Env_Delete", "环境删除失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteSwitchAsync()
    {
        if (SelectedEnvironment == null) return;

        _logService.LogAction("Env_Switch", $"准备切换到环境: {SelectedEnvironment.Name}");
        IsBusy = true;
        BusyMessage = "正在切换环境...";

        try
        {
            await _backupService.CreateBackupAsync($"切换前备份", false);
            await _environmentService.SwitchEnvironmentAsync(SelectedEnvironment.Id);
            _logService.LogAction("Env_Switch", $"环境切换成功: {SelectedEnvironment.Name}");
        }
        catch (Exception ex)
        {
            _logService.LogError("Env_Switch", "环境切换失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteCopyAsync(HostEnvironment env)
    {
        _logService.LogAction("Env_Copy", $"准备复制环境: {env.Name}");
        IsBusy = true;
        BusyMessage = "正在复制环境...";

        try
        {
            await _environmentService.CreateEnvironmentAsync($"{env.Name} - 副本", env.Description, env);
            _logService.LogAction("Env_Copy", "环境复制成功");
        }
        catch (Exception ex)
        {
            _logService.LogError("Env_Copy", "环境复制失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteEditAsync()
    {
        if (SelectedEnvironment == null) return;

        _logService.LogAction("Env_Edit", $"准备编辑环境: {SelectedEnvironment.Name}");
        IsBusy = true;
        BusyMessage = "正在保存环境...";

        try
        {
            SelectedEnvironment.UpdatedAt = DateTime.Now;
            await _environmentService.UpdateEnvironmentAsync(SelectedEnvironment);
            _logService.LogAction("Env_Edit", "环境编辑保存成功");
        }
        catch (Exception ex)
        {
            _logService.LogError("Env_Edit", "环境编辑保存失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteCompareAsync()
    {
        if (SelectedEnvironment == null) return;

        _logService.LogAction("Env_Compare", $"开始对比环境: {SelectedEnvironment.Name}");

        try
        {
            var diffs = await _environmentService.CompareWithSystemHostsAsync(SelectedEnvironment.Id);
            _logService.LogAction("Env_Compare", $"对比完成，差异数: {diffs.Count}");
        }
        catch (Exception ex)
        {
            _logService.LogError("Env_Compare", "环境对比失败", ex.ToString());
        }
    }
}
