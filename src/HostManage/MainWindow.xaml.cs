using System.Collections;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HostManage.Models;
using HostManage.Services;
using HostManage.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HostManage;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IHostFileService _hostFileService;
    private readonly ILogService _logService;
    private readonly ISettingsService _settingsService;
    private readonly DispatcherTimer _clockTimer;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<MainViewModel>();
        _hostFileService = App.Services.GetRequiredService<IHostFileService>();
        _logService = App.Services.GetRequiredService<ILogService>();
        _settingsService = App.Services.GetRequiredService<ISettingsService>();

        DataContext = _viewModel;

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += ClockTimer_Tick;
        _clockTimer.Start();

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            HostsFilePathTextBlock.Text = _hostFileService.GetHostsFilePath();

            var isAdmin = await _hostFileService.IsAdminAsync();
            UpdateAdminStatus(isAdmin);

            UpdateCurrentTime();

            await _viewModel.LoadAsync();
        }
        catch (Exception ex)
        {
            _logService.LogError("MainWindow_Load", "主窗口加载失败", ex.ToString());
            MessageBox.Show($"主窗口加载失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClockTimer_Tick(object? sender, EventArgs e)
    {
        UpdateCurrentTime();
    }

    private void UpdateCurrentTime()
    {
        CurrentTimeTextBlock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private void UpdateAdminStatus(bool isAdmin)
    {
        if (isAdmin)
        {
            AdminStatusText.Text = "管理员";
            AdminStatusBorder.Background = (System.Windows.Media.Brush)FindResource("SuccessBrush");
        }
        else
        {
            AdminStatusText.Text = "普通用户";
            AdminStatusBorder.Background = (System.Windows.Media.Brush)FindResource("WarningBrush");
        }
    }

    private void NavigationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavigationListBox.SelectedItem is not ListBoxItem selectedItem)
            return;

        var tag = selectedItem.Tag?.ToString();
        _logService.LogAction("UI_Navigate", $"切换导航: {tag}");

        switch (tag)
        {
            case "Rules":
                break;
            case "Environments":
                ShowEnvironmentPanel();
                break;
            case "Proxy":
                ShowProxyPanel();
                break;
            case "Backup":
                ShowBackupPanel();
                break;
            case "Logs":
                ShowLogsPanel();
                break;
            case "Settings":
                ShowSettingsPanel();
                break;
        }
    }

    private void ShowEnvironmentPanel()
    {
        var vm = App.Services.GetRequiredService<EnvironmentListViewModel>();
        _ = vm.LoadAsync();
    }

    private void ShowProxyPanel()
    {
        var vm = App.Services.GetRequiredService<ProxyListViewModel>();
        _ = vm.LoadAsync();
    }

    private void ShowBackupPanel()
    {
        var vm = App.Services.GetRequiredService<BackupListViewModel>();
        _ = vm.LoadAsync();
    }

    private void ShowLogsPanel()
    {
        var vm = App.Services.GetRequiredService<LogsViewModel>();
        _ = vm.LoadAsync();
    }

    private void ShowSettingsPanel()
    {
        var vm = App.Services.GetRequiredService<SettingsViewModel>();
        _ = vm.LoadAsync();
    }

    private void RulesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RulesDataGrid.SelectedItems is IList selectedItems)
        {
            var selected = new List<HostRule>();
            foreach (var item in selectedItems)
            {
                if (item is HostRule rule)
                {
                    selected.Add(rule);
                }
            }
            _viewModel.SelectedRules = selected;
        }
    }

    private void RulesDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (e.Row.Item is HostRule rule)
        {
            e.Row.Style = rule.Status switch
            {
                RuleStatus.Active => (Style)FindResource("RuleRowActive"),
                RuleStatus.Inactive => (Style)FindResource("RuleRowInactive"),
                RuleStatus.Conflict => (Style)FindResource("RuleRowConflict"),
                RuleStatus.Invalid => (Style)FindResource("RuleRowInvalid"),
                _ => (Style)FindResource("RuleRowActive")
            };
        }
    }

    private async void DeleteRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is HostRule rule)
        {
            var result = MessageBox.Show(
                $"确定要删除规则 \"{rule.Domain}\" 吗？",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.SelectedRules = new List<HostRule> { rule };
                if (_viewModel.DeleteRuleCommand.CanExecute(null))
                {
                    await _viewModel.DeleteRuleCommand.ExecuteAsync(null);
                }
            }
        }
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0)
                return;

            _logService.LogAction("File_Drop", $"检测到拖放文件: {files.Length} 个",
                string.Join(";", files));

            var importExportService = App.Services.GetRequiredService<IImportExportService>();
            var importedRules = new List<HostRule>();

            foreach (var file in files)
            {
                try
                {
                    var ext = Path.GetExtension(file).ToLower();
                    List<HostRule> rules = ext switch
                    {
                        ".json" => await importExportService.ImportFromJsonAsync(file),
                        ".csv" => await importExportService.ImportFromCsvAsync(file),
                        _ => await importExportService.ImportFromHostsFileAsync(file)
                    };

                    importedRules.AddRange(rules);
                }
                catch (Exception ex)
                {
                    _logService.LogError("File_Import", $"导入文件失败: {file}", ex.ToString());
                    MessageBox.Show($"导入文件失败: {Path.GetFileName(file)}\n{ex.Message}",
                        "导入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            if (importedRules.Count > 0)
            {
                var env = _viewModel.CurrentEnvironment;
                if (env != null)
                {
                    foreach (var rule in importedRules)
                    {
                        env.Rules.Add(rule);
                    }

                    var environmentService = App.Services.GetRequiredService<IEnvironmentService>();
                    await environmentService.SaveEnvironmentsAsync();
                    await _viewModel.LoadAsync();

                    _logService.LogAction("File_Import", $"成功导入 {importedRules.Count} 条规则");
                    MessageBox.Show(
                        $"成功导入 {importedRules.Count} 条规则到环境 \"{env.Name}\"",
                        "导入完成",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
        }
        catch (Exception ex)
        {
            _logService.LogError("File_Drop", "拖放处理失败", ex.ToString());
            MessageBox.Show($"拖放处理失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            _logService.LogAction("App_Closing", "窗口正在关闭，正在保存配置...");

            await _settingsService.SaveSettingsAsync();

            var environmentService = App.Services.GetRequiredService<IEnvironmentService>();
            await environmentService.SaveEnvironmentsAsync();

            _clockTimer.Stop();
        }
        catch (Exception ex)
        {
            _logService.LogError("App_Closing", "关闭时保存配置失败", ex.ToString());
        }
    }
}
