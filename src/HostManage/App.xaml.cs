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
            var environmentService = Services.GetRequiredService<IEnvironmentService>();
            var logService = Services.GetRequiredService<ILogService>();
            var themeService = Services.GetRequiredService<IThemeService>();

            Task.Run(async () =>
            {
                await settingsService.LoadSettingsAsync().ConfigureAwait(false);
                await environmentService.LoadEnvironmentsAsync().ConfigureAwait(false);
            }).GetAwaiter().GetResult();

            logService.LogAction("App_Startup", "应用程序启动");

            themeService.ApplyTheme(settingsService.Current.Theme);

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            var msg = $"应用程序初始化失败:\n{ex.Message}\n\n堆栈跟踪:\n{ex.StackTrace}";
            if (ex.InnerException != null)
            {
                msg += $"\n\n内部异常:\n{ex.InnerException.Message}\n\n{ex.InnerException.StackTrace}";
            }
            MessageBox.Show(msg, "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

        var msg = $"发生未处理的异常:\n{e.Exception.Message}\n\n类型: {e.Exception.GetType().Name}\n\n堆栈跟踪:\n{e.Exception.StackTrace}";
        if (e.Exception.InnerException != null)
        {
            msg += $"\n\n内部异常:\n{e.Exception.InnerException.Message}\n\n{e.Exception.InnerException.StackTrace}";
        }
        MessageBox.Show(msg, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

            var msg = $"发生未处理的域异常:\n{ex.Message}\n\n类型: {ex.GetType().Name}\n\n堆栈跟踪:\n{ex.StackTrace}";
            if (ex.InnerException != null)
            {
                msg += $"\n\n内部异常:\n{ex.InnerException.Message}\n\n{ex.InnerException.StackTrace}";
            }
            MessageBox.Show(msg, "严重错误", MessageBoxButton.OK, MessageBoxImage.Stop);
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
            var logService = Services?.GetService<ILogService>();
            logService?.LogAction("App_Exit", "应用程序关闭");

            Task.Run(async () =>
            {
                var settingsService = Services?.GetService<ISettingsService>();
                if (settingsService != null)
                {
                    await settingsService.SaveSettingsAsync();
                }

                var environmentService = Services?.GetService<IEnvironmentService>();
                if (environmentService != null)
                {
                    await environmentService.SaveEnvironmentsAsync();
                }
            }).Wait(3000);
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
