using System.Windows;
using HostManage.Services;
using HostManage.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HostManage;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        try
        {
            var settingsService = Services.GetRequiredService<ISettingsService>();
            await settingsService.LoadSettingsAsync();

            var environmentService = Services.GetRequiredService<IEnvironmentService>();
            await environmentService.LoadEnvironmentsAsync();

            var logService = Services.GetRequiredService<ILogService>();
            logService.LogAction("App_Startup", "应用程序启动");

            var themeService = Services.GetRequiredService<IThemeService>();
            themeService.ApplyTheme(settingsService.Current.Theme);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"应用程序初始化失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            var settingsService = Services?.GetService<ISettingsService>();
            if (settingsService != null)
            {
                var saveTask = settingsService.SaveSettingsAsync();
                saveTask.Wait(2000);
            }

            var environmentService = Services?.GetService<IEnvironmentService>();
            if (environmentService != null)
            {
                var saveTask = environmentService.SaveEnvironmentsAsync();
                saveTask.Wait(2000);
            }

            var logService = Services?.GetService<ILogService>();
            logService?.LogAction("App_Exit", "应用程序关闭");
        }
        catch
        {
        }

        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(configure =>
        {
            configure.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSingleton<IHostFileService, HostFileService>();
        services.AddSingleton<IEnvironmentService, EnvironmentService>();
        services.AddSingleton<IProxyService, ProxyService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<IValidationService, ValidationService>();
        services.AddSingleton<INetworkService, NetworkService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IImportExportService, ImportExportService>();
        services.AddSingleton<IThemeService, ThemeService>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<EnvironmentListViewModel>();
        services.AddSingleton<ProxyListViewModel>();
        services.AddSingleton<BackupListViewModel>();
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<SettingsViewModel>();

        services.AddTransient<MainWindow>();
    }
}
