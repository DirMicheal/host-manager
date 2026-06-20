using System.Collections;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HostManage.Dialogs;
using HostManage.Models;
using HostManage.Services;
using HostManage.ViewModels;
using HostManage.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace HostManage;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IHostFileService _hostFileService;
    private readonly ILogService _logService;
    private readonly ISettingsService _settingsService;
    private readonly DispatcherTimer _clockTimer;

    private HostRulesPage? _rulesPage;
    private EnvironmentPage? _environmentPage;
    private ProxyPage? _proxyPage;
    private BackupPage? _backupPage;
    LogsPage? _logsPage;
    private SettingsPage? _settingsPage;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<MainViewModel>();
        _hostFileService = App.Services.GetRequiredService<IHostFileService>();
        _logService = App.Services.GetRequiredService<ILogService>();
        _settingsService = App.Services.GetRequiredService<ISettingsService>();

        DataContext = _viewModel;

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
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

            NavigateToRules();
        }
        catch (Exception ex)
        {
            _logService.LogError("MainWindow_Load", "主窗口加载失败", ex.ToString());
            MessageBox.Show($"主窗口加载失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClockTimer_Tick(object? sender, EventArgs e) => UpdateCurrentTime();

    private void UpdateCurrentTime() =>
        CurrentTimeTextBlock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

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
                NavigateToRules();
                break;
            case "Environments":
                NavigateToEnvironments();
                break;
            case "Proxy":
                NavigateToProxy();
                break;
            case "Backup":
                NavigateToBackup();
                break;
            case "Logs":
                NavigateToLogs();
                break;
            case "Settings":
                NavigateToSettings();
                break;
        }
    }

    private void NavigateToRules()
    {
        _rulesPage ??= new HostRulesPage { DataContext = _viewModel };
        MainContentControl.Content = _rulesPage;
    }

    private void NavigateToEnvironments()
    {
        var vm = App.Services.GetRequiredService<EnvironmentListViewModel>();
        _ = vm.LoadAsync();
        _environmentPage ??= new EnvironmentPage();
        _environmentPage.DataContext = vm;
        MainContentControl.Content = _environmentPage;
    }

    private void NavigateToProxy()
    {
        var vm = App.Services.GetRequiredService<ProxyListViewModel>();
        _ = vm.LoadAsync();
        _proxyPage ??= new ProxyPage();
        _proxyPage.DataContext = vm;
        MainContentControl.Content = _proxyPage;
    }

    private void NavigateToBackup()
    {
        var vm = App.Services.GetRequiredService<BackupListViewModel>();
        _ = vm.LoadAsync();
        _backupPage ??= new BackupPage();
        _backupPage.DataContext = vm;
        MainContentControl.Content = _backupPage;
    }

    private void NavigateToLogs()
    {
        var vm = App.Services.GetRequiredService<LogsViewModel>();
        _ = vm.LoadAsync();
        _logsPage ??= new LogsPage();
        _logsPage.DataContext = vm;
        MainContentControl.Content = _logsPage;
    }

    private void NavigateToSettings()
    {
        var vm = App.Services.GetRequiredService<SettingsViewModel>();
        _ = vm.LoadAsync();
        _settingsPage ??= new SettingsPage();
        _settingsPage.DataContext = vm;
        MainContentControl.Content = _settingsPage;
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

            _logService.LogAction("File_Drop", $"检测到拖放文件: {files.Length} 个", string.Join(";", files));

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
                        env.Rules.Add(rule);

                    var environmentService = App.Services.GetRequiredService<IEnvironmentService>();
                    await environmentService.SaveEnvironmentsAsync();
                    await _viewModel.LoadAsync();

                    MessageBox.Show($"成功导入 {importedRules.Count} 条规则到环境 \"{env.Name}\"",
                        "导入完成", MessageBoxButton.OK, MessageBoxImage.Information);
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
            _logService.LogAction("App_Closing", "窗口正在关闭");
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
