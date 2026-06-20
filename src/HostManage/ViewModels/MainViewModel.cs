using System.Collections.ObjectModel;
using System.Windows;
using HostManage.Dialogs;
using HostManage.Models;
using HostManage.Services;
using Microsoft.Win32;

namespace HostManage.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IEnvironmentService _environmentService;
    private readonly IHostFileService _hostFileService;
    private readonly IBackupService _backupService;
    private readonly ILogService _logService;
    private readonly ISettingsService _settingsService;
    private readonly IProxyService _proxyService;
    private readonly IImportExportService _importExportService;
    private readonly IValidationService _validationService;
    private readonly IThemeService _themeService;
    private readonly INetworkService _networkService;

    private string _title = "HostManage";
    private string _subtitle = "Hosts文件管理工具";
    private HostEnvironment? _currentEnvironment;
    private string _searchKeyword = string.Empty;
    private ObservableCollection<HostRule> _allRules = new();
    private ObservableCollection<HostRule> _filteredRules = new();
    private List<HostRule> _selectedRules = new();

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Subtitle
    {
        get => _subtitle;
        set => SetProperty(ref _subtitle, value);
    }

    public HostEnvironment? CurrentEnvironment
    {
        get => _currentEnvironment;
        set => SetProperty(ref _currentEnvironment, value);
    }

    public string SearchKeyword
    {
        get => _searchKeyword;
        set
        {
            if (SetProperty(ref _searchKeyword, value))
                ApplyFilter();
        }
    }

    public ObservableCollection<HostRule> FilteredRules
    {
        get => _filteredRules;
        set
        {
            if (SetProperty(ref _filteredRules, value))
                OnPropertyChanged(nameof(EnabledCount));
        }
    }

    public int EnabledCount => _allRules.Count(r => r.IsEnabled);

    public List<HostRule> SelectedRules
    {
        get => _selectedRules;
        set
        {
            SetProperty(ref _selectedRules, value);
            RaiseSelectionCommands();
        }
    }

    public AsyncRelayCommand AddRuleCommand { get; }
    public AsyncRelayCommand<HostRule> EditRuleCommand { get; }
    public AsyncRelayCommand DeleteRuleCommand { get; }
    public AsyncRelayCommand<HostRule> ToggleRuleCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand SearchCommand { get; }
    public AsyncRelayCommand BatchEnableCommand { get; }
    public AsyncRelayCommand BatchDisableCommand { get; }
    public AsyncRelayCommand BatchDeleteCommand { get; }
    public AsyncRelayCommand ImportCommand { get; }
    public AsyncRelayCommand ExportCommand { get; }
    public AsyncRelayCommand CompareCommand { get; }
    public AsyncRelayCommand CreateEnvironmentCommand { get; }
    public AsyncRelayCommand<HostEnvironment> SwitchEnvironmentCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand OpenLogsCommand { get; }
    public RelayCommand OpenBackupCommand { get; }

    public MainViewModel(
        IEnvironmentService environmentService,
        IHostFileService hostFileService,
        IBackupService backupService,
        ILogService logService,
        ISettingsService settingsService,
        IProxyService proxyService,
        IImportExportService importExportService,
        IValidationService validationService,
        IThemeService themeService,
        INetworkService networkService)
    {
        _environmentService = environmentService;
        _hostFileService = hostFileService;
        _backupService = backupService;
        _logService = logService;
        _settingsService = settingsService;
        _proxyService = proxyService;
        _importExportService = importExportService;
        _validationService = validationService;
        _themeService = themeService;
        _networkService = networkService;

        AddRuleCommand = new AsyncRelayCommand(ExecuteAddRuleAsync, () => CurrentEnvironment != null);
        EditRuleCommand = new AsyncRelayCommand<HostRule>(ExecuteEditRuleAsync, _ => SelectedRules.Count == 1);
        DeleteRuleCommand = new AsyncRelayCommand(ExecuteDeleteRuleAsync, () => SelectedRules.Count > 0);
        ToggleRuleCommand = new AsyncRelayCommand<HostRule>(ExecuteToggleRuleAsync, _ => true);
        RefreshCommand = new AsyncRelayCommand(ExecuteRefreshAsync);
        SearchCommand = new RelayCommand(ApplyFilter);
        BatchEnableCommand = new AsyncRelayCommand(ExecuteBatchEnableAsync, () => CurrentEnvironment != null);
        BatchDisableCommand = new AsyncRelayCommand(ExecuteBatchDisableAsync, () => CurrentEnvironment != null);
        BatchDeleteCommand = new AsyncRelayCommand(ExecuteBatchDeleteAsync, () => SelectedRules.Count > 0);
        ImportCommand = new AsyncRelayCommand(ExecuteImportAsync);
        ExportCommand = new AsyncRelayCommand(ExecuteExportAsync, () => FilteredRules.Count > 0);
        CompareCommand = new AsyncRelayCommand(ExecuteCompareAsync, () => CurrentEnvironment != null);
        CreateEnvironmentCommand = new AsyncRelayCommand(ExecuteCreateEnvironmentAsync);
        SwitchEnvironmentCommand = new AsyncRelayCommand<HostEnvironment>(ExecuteSwitchEnvironmentAsync, env => env != null && !env.IsActive);
        OpenSettingsCommand = new RelayCommand(ExecuteOpenSettings);
        OpenLogsCommand = new RelayCommand(ExecuteOpenLogs);
        OpenBackupCommand = new RelayCommand(ExecuteOpenBackup);
    }

    public override async Task LoadAsync()
    {
        _logService.LogAction("Main_Load", "开始加载主界面数据");
        IsBusy = true;
        BusyMessage = "正在加载数据...";

        try
        {
            await Task.CompletedTask;
            CurrentEnvironment = _environmentService.CurrentEnvironment;
            if (CurrentEnvironment != null)
            {
                _allRules = new ObservableCollection<HostRule>(CurrentEnvironment.Rules);
                ApplyFilter();
            }

            _logService.LogAction("Main_Load", "主界面数据加载完成");
        }
        catch (Exception ex)
        {
            _logService.LogError("Main_Load", "主界面数据加载失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<HostRule> source = _allRules;

        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            var keyword = SearchKeyword.Trim().ToLower();
            source = _allRules.Where(r =>
                (r.IP != null && r.IP.ToLower().Contains(keyword)) ||
                (r.Domain != null && r.Domain.ToLower().Contains(keyword)) ||
                (r.Comment != null && r.Comment.ToLower().Contains(keyword))
            );
        }

        FilteredRules = new ObservableCollection<HostRule>(source.ToList());
    }

    private void RaiseSelectionCommands()
    {
        EditRuleCommand.RaiseCanExecuteChanged();
        DeleteRuleCommand.RaiseCanExecuteChanged();
        BatchEnableCommand.RaiseCanExecuteChanged();
        BatchDisableCommand.RaiseCanExecuteChanged();
        BatchDeleteCommand.RaiseCanExecuteChanged();
    }

    private async Task ExecuteAddRuleAsync()
    {
        _logService.LogAction("Rule_Add", "准备添加Host规则");

        var dialog = new RuleEditDialog();
        dialog.Owner = Application.Current.MainWindow;
        if (dialog.ShowDialog() == true && dialog.Result != null && CurrentEnvironment != null)
        {
            CurrentEnvironment.Rules.Add(dialog.Result);
            _allRules.Add(dialog.Result);
            ApplyFilter();
            await _environmentService.SaveEnvironmentsAsync();
            _logService.LogAction("Rule_Add", $"添加规则成功: {dialog.Result.IP} {dialog.Result.Domain}");
        }
    }

    private async Task ExecuteEditRuleAsync(HostRule? rule)
    {
        if (rule == null) return;
        _logService.LogAction("Rule_Edit", $"准备编辑Host规则: {rule.Domain}");

        var dialog = new RuleEditDialog(rule);
        dialog.Owner = Application.Current.MainWindow;
        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            rule.IP = dialog.Result.IP;
            rule.Domain = dialog.Result.Domain;
            rule.Comment = dialog.Result.Comment;
            rule.IsEnabled = dialog.Result.IsEnabled;
            rule.Status = dialog.Result.IsEnabled ? RuleStatus.Active : RuleStatus.Inactive;
            rule.UpdatedAt = DateTime.Now;
            ApplyFilter();
            await _environmentService.SaveEnvironmentsAsync();
            _logService.LogAction("Rule_Edit", $"编辑规则完成: {rule.IP} {rule.Domain}");
        }
    }

    private async Task ExecuteDeleteRuleAsync()
    {
        _logService.LogAction("Rule_Delete", $"准备删除{SelectedRules.Count}条Host规则");
        if (CurrentEnvironment == null) return;

        var result = MessageBox.Show(
            $"确定要删除选中的 {SelectedRules.Count} 条规则吗？",
            "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        IsBusy = true;
        BusyMessage = "正在删除规则...";

        try
        {
            foreach (var rule in SelectedRules.ToList())
            {
                CurrentEnvironment.Rules.Remove(rule);
                _allRules.Remove(rule);
            }

            ApplyFilter();
            await _environmentService.SaveEnvironmentsAsync();
            _logService.LogAction("Rule_Delete", "规则删除成功");
        }
        catch (Exception ex)
        {
            _logService.LogError("Rule_Delete", "删除Host规则失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteToggleRuleAsync(HostRule? rule)
    {
        if (rule == null) return;
        _logService.LogAction("Rule_Toggle", $"切换规则状态: {rule.Domain}");

        try
        {
            rule.IsEnabled = !rule.IsEnabled;
            rule.Status = rule.IsEnabled ? RuleStatus.Active : RuleStatus.Inactive;
            rule.UpdatedAt = DateTime.Now;
            await _environmentService.SaveEnvironmentsAsync();
            OnPropertyChanged(nameof(EnabledCount));
            _logService.LogAction("Rule_Toggle", $"规则状态切换完成: {rule.IsEnabled}");
        }
        catch (Exception ex)
        {
            _logService.LogError("Rule_Toggle", "切换规则状态失败", ex.ToString());
        }
    }

    private async Task ExecuteRefreshAsync()
    {
        _logService.LogAction("Data_Refresh", "开始刷新数据");
        IsBusy = true;
        BusyMessage = "正在刷新数据...";

        try
        {
            await _environmentService.LoadEnvironmentsAsync();
            CurrentEnvironment = _environmentService.CurrentEnvironment;

            if (CurrentEnvironment != null)
            {
                _allRules = new ObservableCollection<HostRule>(CurrentEnvironment.Rules);
                ApplyFilter();
            }

            _logService.LogAction("Data_Refresh", "数据刷新完成");
        }
        catch (Exception ex)
        {
            _logService.LogError("Data_Refresh", "数据刷新失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteBatchEnableAsync()
    {
        _logService.LogAction("Batch_Enable", "批量启用所有规则");
        if (CurrentEnvironment == null) return;
        IsBusy = true;
        BusyMessage = "正在批量启用...";

        try
        {
            foreach (var rule in _allRules)
            {
                rule.IsEnabled = true;
                rule.Status = RuleStatus.Active;
                rule.UpdatedAt = DateTime.Now;
            }

            ApplyFilter();
            await _environmentService.SaveEnvironmentsAsync();
            _logService.LogAction("Batch_Enable", "批量启用完成");
        }
        catch (Exception ex)
        {
            _logService.LogError("Batch_Enable", "批量启用失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteBatchDisableAsync()
    {
        _logService.LogAction("Batch_Disable", "批量禁用所有规则");
        if (CurrentEnvironment == null) return;
        IsBusy = true;
        BusyMessage = "正在批量禁用...";

        try
        {
            foreach (var rule in _allRules)
            {
                rule.IsEnabled = false;
                rule.Status = RuleStatus.Inactive;
                rule.UpdatedAt = DateTime.Now;
            }

            ApplyFilter();
            await _environmentService.SaveEnvironmentsAsync();
            _logService.LogAction("Batch_Disable", "批量禁用完成");
        }
        catch (Exception ex)
        {
            _logService.LogError("Batch_Disable", "批量禁用失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteBatchDeleteAsync()
    {
        await ExecuteDeleteRuleAsync();
    }

    private async Task ExecuteImportAsync()
    {
        _logService.LogAction("Data_Import", "准备导入Host规则");

        var dialog = new OpenFileDialog
        {
            Title = "导入Host规则",
            Filter = "Hosts文件 (*.hosts;*.txt)|*.hosts;*.txt|JSON文件 (*.json)|*.json|CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var ext = System.IO.Path.GetExtension(dialog.FileName).ToLower();
            List<HostRule> rules = ext switch
            {
                ".json" => await _importExportService.ImportFromJsonAsync(dialog.FileName),
                ".csv" => await _importExportService.ImportFromCsvAsync(dialog.FileName),
                _ => await _importExportService.ImportFromHostsFileAsync(dialog.FileName)
            };

            if (rules.Count > 0 && CurrentEnvironment != null)
            {
                foreach (var rule in rules)
                    CurrentEnvironment.Rules.Add(rule);

                _allRules = new ObservableCollection<HostRule>(CurrentEnvironment.Rules);
                ApplyFilter();
                await _environmentService.SaveEnvironmentsAsync();

                MessageBox.Show($"成功导入 {rules.Count} 条规则", "导入完成",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logService.LogError("Data_Import", "导入失败", ex.ToString());
            MessageBox.Show($"导入失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ExecuteExportAsync()
    {
        _logService.LogAction("Data_Export", $"准备导出{FilteredRules.Count}条Host规则");

        var dialog = new SaveFileDialog
        {
            Title = "导出Host规则",
            Filter = "Hosts文件 (*.hosts)|*.hosts|JSON文件 (*.json)|*.json|CSV文件 (*.csv)|*.csv",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var ext = System.IO.Path.GetExtension(dialog.FileName).ToLower();
            switch (ext)
            {
                case ".json":
                    await _importExportService.ExportToJsonAsync(dialog.FileName, FilteredRules);
                    break;
                case ".csv":
                    await _importExportService.ExportToCsvAsync(dialog.FileName, FilteredRules);
                    break;
                default:
                    await _importExportService.ExportToHostsFileAsync(dialog.FileName, FilteredRules);
                    break;
            }

            MessageBox.Show($"成功导出 {FilteredRules.Count} 条规则", "导出完成",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logService.LogError("Data_Export", "导出失败", ex.ToString());
            MessageBox.Show($"导出失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ExecuteCompareAsync()
    {
        _logService.LogAction("Env_Compare", $"开始与系统Hosts对比: {CurrentEnvironment?.Name}");
        if (CurrentEnvironment == null) return;

        try
        {
            var diffs = await _environmentService.CompareWithSystemHostsAsync(CurrentEnvironment.Id);

            var compareDialog = new CompareDialog(diffs);
            compareDialog.Owner = Application.Current.MainWindow;
            compareDialog.ShowDialog();

            _logService.LogAction("Env_Compare", $"对比完成，差异数: {diffs.Count}");
        }
        catch (Exception ex)
        {
            _logService.LogError("Env_Compare", "对比失败", ex.ToString());
            MessageBox.Show($"对比失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ExecuteCreateEnvironmentAsync()
    {
        _logService.LogAction("Env_Create", "准备创建环境");

        var dialog = new EnvironmentDialog();
        dialog.Owner = Application.Current.MainWindow;
        if (dialog.ShowDialog() == true)
        {
            await _environmentService.CreateEnvironmentAsync(dialog.EnvironmentName, dialog.EnvironmentDescription);
            CurrentEnvironment = _environmentService.CurrentEnvironment;
        }
    }

    private async Task ExecuteSwitchEnvironmentAsync(HostEnvironment env)
    {
        _logService.LogAction("Env_Switch", $"准备切换环境: {env.Name}");

        try
        {
            await _environmentService.SwitchEnvironmentAsync(env.Id);
            CurrentEnvironment = _environmentService.CurrentEnvironment;

            if (CurrentEnvironment != null)
            {
                _allRules = new ObservableCollection<HostRule>(CurrentEnvironment.Rules);
                ApplyFilter();
            }

            _logService.LogAction("Env_Switch", $"环境切换成功: {env.Name}");
        }
        catch (Exception ex)
        {
            _logService.LogError("Env_Switch", "环境切换失败", ex.ToString());
            MessageBox.Show($"环境切换失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecuteOpenSettings()
    {
        _logService.LogAction("UI_Navigate", "打开设置界面");
        var window = Application.Current.MainWindow;
        if (window is MainWindow mw)
            mw.NavigationListBox.SelectedIndex = 5;
    }

    private void ExecuteOpenLogs()
    {
        _logService.LogAction("UI_Navigate", "打开日志界面");
        var window = Application.Current.MainWindow;
        if (window is MainWindow mw)
            mw.NavigationListBox.SelectedIndex = 4;
    }

    private void ExecuteOpenBackup()
    {
        _logService.LogAction("UI_Navigate", "打开备份界面");
        var window = Application.Current.MainWindow;
        if (window is MainWindow mw)
            mw.NavigationListBox.SelectedIndex = 3;
    }
}
