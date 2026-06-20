using System.Diagnostics;
using Microsoft.Win32;

namespace HostManage.Helpers;

public static class RegistryHelper
{
    private const string AutoStartRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ProxyRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    public static bool SetAutoStart(bool enable, string appName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(appName))
                throw new ArgumentException("应用名不能为空", nameof(appName));

            using var key = Registry.CurrentUser.OpenSubKey(AutoStartRegistryPath, true);
            if (key == null)
                return false;

            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

            if (enable)
            {
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(appName, $"\"{exePath}\"");
                }
            }
            else
            {
                if (key.GetValue(appName) != null)
                {
                    key.DeleteValue(appName, false);
                }
            }

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool IsAutoStartEnabled(string appName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(appName))
                return false;

            using var key = Registry.CurrentUser.OpenSubKey(AutoStartRegistryPath, false);
            if (key == null)
                return false;

            var value = key.GetValue(appName);
            return value != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool SetSystemProxy(bool enable, string? proxyServer)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ProxyRegistryPath, true);
            if (key == null)
                return false;

            key.SetValue("ProxyEnable", enable ? 1 : 0, RegistryValueKind.DWord);

            if (enable && !string.IsNullOrWhiteSpace(proxyServer))
            {
                key.SetValue("ProxyServer", proxyServer, RegistryValueKind.String);
            }

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static (bool Enabled, string ProxyServer) GetSystemProxy()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ProxyRegistryPath, false);
            if (key == null)
                return (false, string.Empty);

            var enableValue = key.GetValue("ProxyEnable");
            var proxyValue = key.GetValue("ProxyServer");

            bool enabled = enableValue != null && Convert.ToInt32(enableValue) == 1;
            string proxyServer = proxyValue?.ToString() ?? string.Empty;

            return (enabled, proxyServer);
        }
        catch (Exception)
        {
            return (false, string.Empty);
        }
    }
}
