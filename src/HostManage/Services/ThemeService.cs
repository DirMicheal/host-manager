using System.Windows;
using HostManage.Models;

namespace HostManage.Services;

public class ThemeService : IThemeService
{
    private const string LightThemeUri = "pack://application:,,,/ThemeResources/LightTheme.xaml";
    private const string DarkThemeUri = "pack://application:,,,/ThemeResources/DarkTheme.xaml";

    public void ApplyTheme(ThemeType theme)
    {
        try
        {
            var app = Application.Current;
            if (app == null)
                return;

            var resources = app.Resources;

            var existingLight = resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("LightTheme.xaml"));
            var existingDark = resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("DarkTheme.xaml"));

            if (existingLight != null)
                resources.MergedDictionaries.Remove(existingLight);
            if (existingDark != null)
                resources.MergedDictionaries.Remove(existingDark);

            var themeUri = theme == ThemeType.Dark ? DarkThemeUri : LightThemeUri;
            var themeDictionary = new ResourceDictionary { Source = new Uri(themeUri, UriKind.RelativeOrAbsolute) };
            resources.MergedDictionaries.Add(themeDictionary);
        }
        catch
        {
        }
    }

    public object GetThemeResources()
    {
        try
        {
            var app = Application.Current;
            if (app == null)
                return new ResourceDictionary();

            return app.Resources;
        }
        catch
        {
            return new ResourceDictionary();
        }
    }
}
