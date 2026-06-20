using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using HostManage.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace HostManage.Services;

public class LogService : ILogService, IDisposable
{
    private const int MaxLogsInMemory = 10000;
    private const int PersistIntervalMs = 10000;

    private readonly ILogger<LogService> _logger;
    private readonly object _lock = new();
    private readonly string _logsFilePath;
    private readonly Queue<OperationLog> _pendingLogs = new();
    private Timer? _persistTimer;
    private bool _disposed;
    private bool _loaded;

    public ObservableCollection<OperationLog> Logs { get; private set; } = new();

    public LogService(ILogger<LogService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HostManage");
        if (!Directory.Exists(appDataPath))
        {
            Directory.CreateDirectory(appDataPath);
        }
        _logsFilePath = Path.Combine(appDataPath, "logs.json");

        LoadLogsSync();

        _persistTimer = new Timer(PersistTimerCallback, null, PersistIntervalMs, PersistIntervalMs);
    }

    private void LoadLogsSync()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_logsFilePath))
                {
                    var json = File.ReadAllText(_logsFilePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var list = JsonConvert.DeserializeObject<List<OperationLog>>(json);
                        if (list != null && list.Count > 0)
                        {
                            list = list.OrderByDescending(x => x.Timestamp).Take(MaxLogsInMemory).ToList();
                            Logs = new ObservableCollection<OperationLog>(list);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载日志文件失败");
                Logs = new ObservableCollection<OperationLog>();
            }
            finally
            {
                _loaded = true;
                Monitor.PulseAll(_lock);
            }
        }
    }

    public void LogInfo(string action, string message, string? details = null)
    {
        AddLog(Models.LogLevel.Info, action, message, details);
    }

    public void LogWarn(string action, string message, string? details = null)
    {
        AddLog(Models.LogLevel.Warn, action, message, details);
    }

    public void LogError(string action, string message, string? details = null)
    {
        AddLog(Models.LogLevel.Error, action, message, details);
    }

    public void LogAction(string action, string message, string? details = null)
    {
        AddLog(Models.LogLevel.Action, action, message, details);
    }

    private void AddLog(Models.LogLevel level, string action, string message, string? details)
    {
        var log = new OperationLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.Now,
            Level = level,
            Action = action ?? string.Empty,
            Message = message ?? string.Empty,
            UserName = Environment.UserName,
            MachineName = Environment.MachineName,
            Details = details ?? string.Empty
        };

        lock (_lock)
        {
            while (!_loaded && !_disposed)
            {
                Monitor.Wait(_lock, 50);
            }

            if (_disposed) return;

            void InsertLog()
            {
                Logs.Insert(0, log);
                while (Logs.Count > MaxLogsInMemory)
                {
                    Logs.RemoveAt(Logs.Count - 1);
                }
            }

            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(InsertLog, DispatcherPriority.Background);
            }
            else
            {
                InsertLog();
            }

            _pendingLogs.Enqueue(log);
        }
    }

    private async void PersistTimerCallback(object? state)
    {
        try
        {
            await PersistPendingLogsAsync().ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private async Task PersistPendingLogsAsync()
    {
        List<OperationLog>? allLogs = null;
        lock (_lock)
        {
            if (_pendingLogs.Count == 0 || _disposed)
            {
                return;
            }
            allLogs = Logs.ToList();
            _pendingLogs.Clear();
        }

        try
        {
            var json = JsonConvert.SerializeObject(allLogs, Formatting.Indented);
            await File.WriteAllTextAsync(_logsFilePath, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "持久化日志失败");
        }
    }

    public Task<List<OperationLog>> SearchLogsAsync(string keyword, DateTime? start = null, DateTime? end = null)
    {
        lock (_lock)
        {
            IEnumerable<OperationLog> query = Logs;

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                query = query.Where(x =>
                    (!string.IsNullOrEmpty(x.Message) && x.Message.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(x.Action) && x.Action.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(x.Details) && x.Details.Contains(kw, StringComparison.OrdinalIgnoreCase)));
            }

            if (start.HasValue)
            {
                query = query.Where(x => x.Timestamp >= start.Value);
            }

            if (end.HasValue)
            {
                query = query.Where(x => x.Timestamp <= end.Value);
            }

            return Task.FromResult(query.ToList());
        }
    }

    public async Task ExportLogsAsync(string filePath, IEnumerable<OperationLog> logs)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(logs);

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".json")
        {
            await ExportAsJsonAsync(filePath, logs).ConfigureAwait(false);
        }
        else if (ext == ".csv")
        {
            await ExportAsCsvAsync(filePath, logs).ConfigureAwait(false);
        }
        else
        {
            throw new ArgumentException("不支持的文件格式，仅支持 .json 或 .csv", nameof(filePath));
        }
    }

    private static async Task ExportAsJsonAsync(string filePath, IEnumerable<OperationLog> logs)
    {
        var json = JsonConvert.SerializeObject(logs.ToList(), Formatting.Indented);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8).ConfigureAwait(false);
    }

    private static async Task ExportAsCsvAsync(string filePath, IEnumerable<OperationLog> logs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,Timestamp,Level,Action,Message,UserName,MachineName,Details");

        foreach (var log in logs)
        {
            sb.Append(CsvEscape(log.Id.ToString()));
            sb.Append(',');
            sb.Append(CsvEscape(log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")));
            sb.Append(',');
            sb.Append(CsvEscape(log.Level.ToString()));
            sb.Append(',');
            sb.Append(CsvEscape(log.Action));
            sb.Append(',');
            sb.Append(CsvEscape(log.Message));
            sb.Append(',');
            sb.Append(CsvEscape(log.UserName));
            sb.Append(',');
            sb.Append(CsvEscape(log.MachineName));
            sb.Append(',');
            sb.AppendLine(CsvEscape(log.Details));
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), new UTF8Encoding(true)).ConfigureAwait(false);
    }

    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    public async Task<int> CleanupOldLogsAsync(int retentionDays = 90)
    {
        if (retentionDays <= 0)
        {
            throw new ArgumentException("保留天数必须大于0", nameof(retentionDays));
        }

        int removedCount = 0;
        List<OperationLog> allLogs;

        lock (_lock)
        {
            var cutoff = DateTime.Now.AddDays(-retentionDays);
            var toRemove = Logs.Where(x => x.Timestamp < cutoff).ToList();

            void RemoveLogs()
            {
                foreach (var log in toRemove)
                {
                    Logs.Remove(log);
                    removedCount++;
                }
            }

            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(RemoveLogs, DispatcherPriority.Background);
            }
            else
            {
                RemoveLogs();
            }

            allLogs = Logs.ToList();
        }

        if (removedCount > 0)
        {
            try
            {
                var json = JsonConvert.SerializeObject(allLogs, Formatting.Indented);
                await File.WriteAllTextAsync(_logsFilePath, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理日志后保存失败");
            }
        }

        return removedCount;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (disposing)
        {
            _persistTimer?.Dispose();
            _persistTimer = null;

            try
            {
                Task.Run(async () => await PersistPendingLogsAsync().ConfigureAwait(false))
                    .Wait(2000);
            }
            catch
            {
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
