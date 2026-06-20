using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using HostManage.Services;

namespace HostManage.Dialogs;

public partial class AboutDialog : Window
{
    private readonly ISettingsService? _settingsService;

    public string AppName { get; set; } = "Host 管理器";
    public string Copyright { get; set; } = "Copyright © 2026 HostManage. All rights reserved.";
    public string WebsiteUrl { get; set; } = "https://www.hostmanage.app";

    public event EventHandler? CheckUpdateRequested;

    public AboutDialog()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += OnLoaded;
    }

    public AboutDialog(ISettingsService settingsService) : this()
    {
        _settingsService = settingsService;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var assembly = Assembly.GetExecutingAssembly();

        if (string.IsNullOrEmpty(AppName) || AppName == "Host 管理器")
        {
            var productAttr = assembly.GetCustomAttribute<AssemblyProductAttribute>();
            if (productAttr != null && !string.IsNullOrEmpty(productAttr.Product))
            {
                AppName = productAttr.Product;
            }
        }

        var version = assembly.GetName().Version;
        VersionText.Text = version != null ? $"版本 v{version.Major}.{version.Minor}.{version.Build}" : "版本 v1.0.0";

        if (string.IsNullOrEmpty(Copyright) || Copyright.StartsWith("Copyright © 2026"))
        {
            var copyrightAttr = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
            if (copyrightAttr != null && !string.IsNullOrEmpty(copyrightAttr.Copyright))
            {
                Copyright = copyrightAttr.Copyright;
            }
        }
        CopyrightText.Text = Copyright;

        WebsiteLink.Text = WebsiteUrl;
    }

    private void WebsiteLink_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(WebsiteUrl))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = WebsiteUrl,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开网页：{ex.Message}\n\n请手动访问：{WebsiteUrl}",
                "打开网页失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateRequested?.Invoke(this, EventArgs.Empty);

        var updateDialog = new UpdateDialog(_settingsService!);
        updateDialog.Owner = this;
        updateDialog.ShowDialog();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
