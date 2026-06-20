using System.Collections.ObjectModel;
using HostManage.Models;

namespace HostManage.Services;

public interface ILogService
{
    ObservableCollection<OperationLog> Logs { get; }

    void LogInfo(string action, string message, string? details = null);

    void LogWarn(string action, string message, string? details = null);

    void LogError(string action, string message, string? details = null);

    void LogAction(string action, string message, string? details = null);

    Task<List<OperationLog>> SearchLogsAsync(string keyword, DateTime? start = null, DateTime? end = null);

    Task ExportLogsAsync(string filePath, IEnumerable<OperationLog> logs);

    Task<int> CleanupOldLogsAsync(int retentionDays = 90);
}
