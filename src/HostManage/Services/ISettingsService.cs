using HostManage.Models;

namespace HostManage.Services;

public interface ISettingsService
{
    event EventHandler? SettingsChanged;

    AppSettings Current { get; }

    Task LoadSettingsAsync();

    Task SaveSettingsAsync();

    void UpdateTheme(ThemeType theme);
}
