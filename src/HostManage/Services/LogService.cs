using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using HostManage.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace HostManage.Services;

public class LogService : ILogService, IDisposable
{
    private const int MaxLogsInMemory = 10000;
    private const int PersistIntervalMs = 10000;

    private readonly ILogger<LogService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly string _logsFilePath;
    private readonly Queue<OperationLog> _pendingLogs = new();
    private readonly Timer _persistTimer;
    private bool _disposed;

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

        _persistTimer = new Timer(PersistTimerCallback, null, PersistIntervalMs, PersistIntervalMs);

        _ = LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (File.Exists(_logsFilePath))
            {
                var json = await File.ReadAllTextAsync(_logsFilePath);
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
            _semaphore.Release();
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

        _semaphore.Wait();
        try
        {
            Logs.Insert(0, log);
            while (Logs.Count > MaxLogsInMemory)
            {
                Logs.RemoveAt(Logs.Count - 1);
            }
            _pendingLogs.Enqueue(log);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async void PersistTimerCallback(object? state)
    {
        await PersistPendingLogsAsync();
    }

    private async Task PersistPendingLogsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_pendingLogs.Count == 0)
            {
                return;
            }

            var allLogs = Logs.ToList();
            var json = JsonConvert.SerializeObject(allLogs, Formatting.Indented);
            await File.WriteAllTextAsync(_logsFilePath, json);
            _pendingLogs.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "持久化日志失败");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<OperationLog>> SearchLogsAsync(string keyword, DateTime? start = null, DateTime? end = null)
    {
        await _semaphore.WaitAsync();
        try
        {
            var query = Logs.AsEnumerable();

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

            return query.ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ExportLogsAsync(string filePath, IEnumerable<OperationLog> logs)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(logs);

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".json")
        {
            await ExportAsJsonAsync(filePath, logs);
        }
        else if (ext == ".csv")
        {
            await ExportAsCsvAsync(filePath, logs);
        }
        else
        {
            throw new ArgumentException("不支持的文件格式，仅支持 .json 或 .csv", nameof(filePath));
        }
    }

    private static async Task ExportAsJsonAsync(string filePath, IEnumerable<OperationLog> logs)
    {
        var json = JsonConvert.SerializeObject(logs.ToList(), Formatting.Indented);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
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

        await File.WriteAllTextAsync(filePath, sb.ToString(), new UTF8Encoding(true));
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

        await _semaphore.WaitAsync();
        try
        {
            var cutoff = DateTime.Now.AddDays(-retentionDays);
            var toRemove = Logs.Where(x => x.Timestamp < cutoff).ToList();
            var removedCount = 0;

            foreach (var log in toRemove)
            {
                Logs.Remove(log);
                removedCount++;
            }

            if (removedCount > 0)
            {
                try
                {
                    var allLogs = Logs.ToList();
                    var json = JsonConvert.SerializeObject(allLogs, Formatting.Indented);
                    await File.WriteAllTextAsync(_logsFilePath, json);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "清理日志后保存失败");
                }
            }

            return removedCount;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _persistTimer.Dispose();
            PersistPendingLogsAsync().GetAwaiter().GetResult();
            _semaphore.Dispose();
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
