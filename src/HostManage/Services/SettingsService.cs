using System.IO;
using HostManage.Models;
using Newtonsoft.Json;

namespace HostManage.Services;

public class SettingsService : ISettingsService
{
    private static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HostManage");
    private static readonly string SettingsFilePath = Path.Combine(AppDataPath, "settings.json");

    public event EventHandler? SettingsChanged;

    public AppSettings Current { get; private set; }

    public SettingsService()
    {
        Current = CreateDefaultSettings();
    }

    private static AppSettings CreateDefaultSettings()
    {
        return new AppSettings
        {
            Theme = ThemeType.Light,
            AutoBackup = true,
            MaxBackupCount = 30,
            LogRetentionDays = 90,
            AutoStart = false,
            AutoApplyEnvOnStartup = true,
            CheckUpdate = true
        };
    }

    private static void EnsureAppDataDirectory()
    {
        if (!Directory.Exists(AppDataPath))
        {
            Directory.CreateDirectory(AppDataPath);
        }
    }

    public async Task LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = await File.ReadAllTextAsync(SettingsFilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var loaded = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (loaded != null)
                    {
                        Current = loaded;
                        return;
                    }
                }
            }
            Current = CreateDefaultSettings();
            await SaveSettingsAsync();
        }
        catch
        {
            Current = CreateDefaultSettings();
        }
    }

    public async Task SaveSettingsAsync()
    {
        try
        {
            EnsureAppDataDirectory();
            var json = JsonConvert.SerializeObject(Current, Formatting.Indented);
            await File.WriteAllTextAsync(SettingsFilePath, json);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
        }
    }

    private void SaveSettingsCore()
    {
        try
        {
            EnsureAppDataDirectory();
            var json = JsonConvert.SerializeObject(Current, Formatting.Indented);
            File.WriteAllText(SettingsFilePath, json);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
        }
    }

    public void UpdateTheme(ThemeType theme)
    {
        Current.Theme = theme;
        SaveSettingsCore();
    }
}
