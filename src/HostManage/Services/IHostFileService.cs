using HostManage.Models;

namespace HostManage.Services;

public interface IHostFileService
{
    event EventHandler? HostsFileChanged;

    string GetHostsFilePath();

    Task<List<HostRule>> ReadSystemHostsAsync();

    Task WriteSystemHostsAsync(List<HostRule> rules, bool backupFirst = true);

    Task<bool> RequestAdminPrivilegesAsync();

    Task<bool> IsAdminAsync();

    IDisposable AcquireFileLock();
}
