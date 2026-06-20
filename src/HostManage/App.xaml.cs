using System.Windows;
using System.Windows.Threading;
using HostManage.Services;
using HostManage.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HostManage;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        try
        {
            var settingsService = Services.GetRequiredService<ISettingsService>();
            settingsService.LoadSettingsAsync().Wait();

            var environmentService = Services.GetRequiredService<IEnvironmentService>();
            environmentService.LoadEnvironmentsAsync().Wait();

            var logService = Services.GetRequiredService<ILogService>();
            logService.LogAction("App_Startup", "应用程序启动");

            var themeService = Services.GetRequiredService<IThemeService>();
            themeService.ApplyTheme(settingsService.Current.Theme);

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"应用程序初始化失败:\n{ex.Message}\n\n{ex.InnerException?.Message}",
                "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var logService = Services?.GetService<ILogService>();
            logService?.LogError("UnhandledException", e.Exception.Message, e.Exception.ToString());
        }
        catch { }

        MessageBox.Show($"发生未处理的异常:\n{e.Exception.Message}",
            "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            try
            {
                var logService = Services?.GetService<ILogService>();
                logService?.LogError("DomainUnhandledException", ex.Message, ex.ToString());
            }
            catch { }
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            var logService = Services?.GetService<ILogService>();
            logService?.LogError("UnobservedTaskException", e.Exception?.InnerException?.Message ?? "Unknown",
                e.Exception?.ToString() ?? "");
        }
        catch { }
        e.SetObserved();
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
