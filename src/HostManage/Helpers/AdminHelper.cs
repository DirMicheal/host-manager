using System.Diagnostics;
using System.Security.Principal;

namespace HostManage.Helpers;

public static class AdminHelper
{
    public static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public static bool RestartAsAdmin()
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty,
                Verb = "runas"
            };

            Process.Start(processStartInfo);
            Environment.Exit(0);
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static Process? RunElevated(string fileName, string arguments = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("文件名不能为空", nameof(fileName));

            var processStartInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = fileName,
                Arguments = arguments,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Normal
            };

            return Process.Start(processStartInfo);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
