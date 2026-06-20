using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using HostManage.Models;
using HostManage.Services;
using MaterialDesignThemes.Wpf;

namespace HostManage.Dialogs;

public enum UpdateDialogResult
{
    UpdateNow,
    UpdateLater,
    Cancel
}

public class ChangelogItem
{
    public string Text { get; set; } = string.Empty;
    public ChangelogType Type { get; set; } = ChangelogType.New;
}

public enum ChangelogType
{
    New,
    Fix,
    Improvement
}

public partial class UpdateDialog : Window
{
    private readonly ISettingsService? _settingsService;
    private readonly HttpClient _httpClient = new();
    private bool _isDownloading = false;
    private string _tempInstallerPath = string.Empty;

    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public long FileSize { get; set; } = 0;
    public List<ChangelogItem> Changelog { get; set; } = new();
    public UpdateDialogResult Result { get; private set; } = UpdateDialogResult.Cancel;

    public UpdateDialog()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += OnLoaded;
    }

    public UpdateDialog(ISettingsService settingsService) : this()
    {
        _settingsService = settingsService;
        AutoUpdateCheckBox.IsChecked = settingsService.Current.CheckUpdate;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(CurrentVersion))
        {
            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
            CurrentVersion = $"v{assemblyVersion?.Major}.{assemblyVersion?.Minor}.{assemblyVersion?.Build}";
        }

        CurrentVersionText.Text = CurrentVersion;
        if (!string.IsNullOrEmpty(LatestVersion))
        {
            LatestVersionText.Text = LatestVersion;
        }

        if (Changelog.Count > 0)
        {
            RenderChangelog();
        }

        AutoUpdateCheckBox.Checked += AutoUpdateCheckBox_Checked;
        AutoUpdateCheckBox.Unchecked += AutoUpdateCheckBox_Unchecked;
    }

    private void RenderChangelog()
    {
        ChangelogPanel.Children.Clear();

        for (int i = 0; i < Changelog.Count; i++)
        {
            var item = Changelog[i];
            var bulletDecorator = new BulletDecorator
            {
                Margin = new Thickness(0, 0, 0, i == Changelog.Count - 1 ? 0 : 10)
            };

            PackIconKind iconKind;
            Color iconColor;

            switch (item.Type)
            {
                case ChangelogType.New:
                    iconKind = PackIconKind.CheckCircleOutline;
                    iconColor = Color.FromRgb(0x4C, 0xAF, 0x50);
                    break;
                case ChangelogType.Fix:
                    iconKind = PackIconKind.AlertCircleOutline;
                    iconColor = Color.FromRgb(0xFF, 0x98, 0x00);
                    break;
                case ChangelogType.Improvement:
                default:
                    iconKind = PackIconKind.ArrowUpCircleOutline;
                    iconColor = Color.FromRgb(0x21, 0x96, 0xF3);
                    break;
            }

            bulletDecorator.Bullet = new PackIcon
            {
                Kind = iconKind,
                Width = 16,
                Height = 16,
                Foreground = new SolidColorBrush(iconColor),
                VerticalAlignment = VerticalAlignment.Center
            };

            bulletDecorator.Child = new TextBlock
            {
                Text = item.Text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0x42, 0x42)),
                Margin = new Thickness(8, 0, 0, 0)
            };

            ChangelogPanel.Children.Add(bulletDecorator);
        }
    }

    private async void AutoUpdateCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (_settingsService != null)
        {
            _settingsService.Current.CheckUpdate = true;
            await _settingsService.SaveSettingsAsync();
        }
    }

    private async void AutoUpdateCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_settingsService != null)
        {
            _settingsService.Current.CheckUpdate = false;
            await _settingsService.SaveSettingsAsync();
        }
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isDownloading) return;

        if (string.IsNullOrEmpty(DownloadUrl))
        {
            MessageBox.Show("更新地址未配置，请稍后重试或手动下载。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _isDownloading = true;
        UpdateButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        DownloadProgressGrid.Visibility = Visibility.Visible;

        try
        {
            _tempInstallerPath = Path.Combine(Path.GetTempPath(), $"HostManage_Update_{LatestVersion?.Trim('v')}.exe");
            using var response = await _httpClient.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = FileSize > 0 ? FileSize : response.Content.Headers.ContentLength ?? 0;
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(_tempInstallerPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    var progress = (double)totalRead / totalBytes * 100;
                    Dispatcher.Invoke(() =>
                    {
                        DownloadProgressBar.Value = progress;
                        DownloadPercentText.Text = $"{progress:F1}%";
                        DownloadSizeText.Text = $"{FormatSize(totalRead)} / {FormatSize(totalBytes)}";
                    });
                }
            }

            DownloadStatusText.Text = "下载完成，正在准备安装...";
            DownloadProgressBar.Value = 100;
            DownloadPercentText.Text = "100%";

            await System.Threading.Tasks.Task.Delay(500);

            var mbResult = MessageBox.Show(
                "下载完成！需要重启应用并安装更新。是否立即安装？\n\n注意：安装过程中请关闭所有正在运行的Host管理器实例。",
                "下载完成",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (mbResult == MessageBoxResult.Yes)
            {
                Result = UpdateDialogResult.UpdateNow;
                StartInstallAndClose();
            }
            else
            {
                _isDownloading = false;
                UpdateButton.IsEnabled = true;
                UpdateButton.Content = "安装更新";
                LaterButton.IsEnabled = true;
                DownloadStatusText.Text = $"更新已下载至：{Path.GetFileName(_tempInstallerPath)}";
            }
        }
        catch (Exception ex)
        {
            _isDownloading = false;
            UpdateButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
            DownloadStatusText.Text = $"下载失败：{ex.Message}";
            DownloadProgressGrid.Visibility = Visibility.Visible;
            MessageBox.Show($"下载更新时出错：{ex.Message}\n\n请稍后重试或手动下载更新。",
                "下载失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StartInstallAndClose()
    {
        try
        {
            if (File.Exists(_tempInstallerPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _tempInstallerPath,
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
        }
        catch
        {
            Process.Start("explorer.exe", Path.GetDirectoryName(_tempInstallerPath) ?? string.Empty);
        }

        DialogResult = true;
        Close();

        Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            await System.Threading.Tasks.Task.Delay(200);
            Application.Current.Shutdown();
        });
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size:F2} {units[unit]}";
    }

    private void LaterButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isDownloading)
        {
            var mbResult = MessageBox.Show("正在下载更新，确定要取消吗？", "确认",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (mbResult != MessageBoxResult.Yes) return;
        }

        Result = UpdateDialogResult.UpdateLater;
        DialogResult = false;
        Close();
    }
}
