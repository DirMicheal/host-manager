using HostManage.Models;

namespace HostManage.Services;

public interface IThemeService
{
    void ApplyTheme(ThemeType theme);

    object GetThemeResources();
}
