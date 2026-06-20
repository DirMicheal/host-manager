using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using HostManage.Models;
using Microsoft.Extensions.Logging;

namespace HostManage.Services;

public partial class HostFileService : IHostFileService, IDisposable
{
    private const string MutexName = "Global\\HostManage_HostsFileLock";
    private const string FileLockMutexName = "Global\\HostManage_HostsFileWriteLock";

    private readonly ILogger<HostFileService> _logger;
    private readonly ILogService _logService;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private FileSystemWatcher? _fileWatcher;
    private bool _disposed;

    public event EventHandler? HostsFileChanged;

    public HostFileService(ILogger<HostFileService> logger, ILogService logService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        InitializeFileWatcher();
    }

    private void InitializeFileWatcher()
    {
        try
        {
            var hostsPath = GetHostsFilePath();
            var directory = Path.GetDirectoryName(hostsPath);
            var fileName = Path.GetFileName(hostsPath);

            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                _fileWatcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };
                _fileWatcher.Changed += OnHostsFileChanged;
                _fileWatcher.Created += OnHostsFileChanged;
                _fileWatcher.Deleted += OnHostsFileChanged;
                _fileWatcher.Renamed += OnHostsFileChanged;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "初始化 hosts 文件监控失败");
        }
    }

    private void OnHostsFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            _logger.LogInformation("检测到 hosts 文件变更: {ChangeType}", e.ChangeType);
            HostsFileChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理 hosts 文件变更事件时出错");
        }
    }

    public string GetHostsFilePath()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "drivers", "etc", "hosts");
        }

        return "/etc/hosts";
    }

    public async Task<List<HostRule>> ReadSystemHostsAsync()
    {
        var rules = new List<HostRule>();
        var hostsPath = GetHostsFilePath();

        _logger.LogInformation("开始读取 hosts 文件: {Path}", hostsPath);
        _logService.LogInfo("读取Hosts", "开始读取系统 hosts 文件", hostsPath);

        try
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);

            if (!File.Exists(hostsPath))
            {
                _logger.LogWarning("hosts 文件不存在: {Path}", hostsPath);
                _logService.LogWarn("读取Hosts", "hosts 文件不存在", hostsPath);
                return rules;
            }

            using var mutex = new Mutex(false, MutexName);
            var mutexAcquired = false;
            try
            {
                mutexAcquired = mutex.WaitOne(5000);
                if (!mutexAcquired)
                {
                    _logger.LogWarning("获取 hosts 文件读取互斥锁超时");
                }

                var lines = await File.ReadAllLinesAsync(hostsPath, Encoding.UTF8).ConfigureAwait(false);
                var lineNumber = 0;

                foreach (var rawLine in lines)
                {
                    lineNumber++;
                    var rule = ParseLine(rawLine, lineNumber);
                    if (rule != null)
                    {
                        rules.Add(rule);
                    }
                }
            }
            finally
            {
                if (mutexAcquired)
                {
                    mutex.ReleaseMutex();
                }
            }

            _logger.LogInformation("成功读取 hosts 文件，共 {Count} 条规则", rules.Count);
            _logService.LogInfo("读取Hosts", $"成功读取 hosts 文件，共 {rules.Count} 条规则");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "无权限读取 hosts 文件");
            _logService.LogError("读取Hosts", "无权限读取 hosts 文件，请以管理员身份运行", ex.Message);
            throw;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "读取 hosts 文件时发生 IO 错误");
            _logService.LogError("读取Hosts", "读取 hosts 文件失败", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取 hosts 文件时发生未预期的错误");
            _logService.LogError("读取Hosts", "读取 hosts 文件时发生错误", ex.Message);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }

        return rules;
    }

    private static HostRule? ParseLine(string rawLine, int lineNumber)
    {
        var line = rawLine.Trim();

        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var isDisabled = false;
        var workingLine = line;

        if (workingLine.StartsWith('#'))
        {
            var afterHash = workingLine[1..].TrimStart();
            if (IsLikelyHostEntry(afterHash))
            {
                isDisabled = true;
                workingLine = afterHash;
            }
            else
            {
                return null;
            }
        }

        string comment = string.Empty;
        var commentIndex = FindCommentIndex(workingLine);
        if (commentIndex >= 0)
        {
            comment = workingLine[(commentIndex + 1)..].Trim();
            workingLine = workingLine[..commentIndex].Trim();
        }

        if (string.IsNullOrWhiteSpace(workingLine))
        {
            return null;
        }

        var parts = Regex.Split(workingLine, @"[\s\t]+").Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

        if (parts.Count < 2)
        {
            return new HostRule
            {
                IP = workingLine,
                Domain = string.Empty,
                Comment = comment,
                IsEnabled = !isDisabled,
                Status = RuleStatus.Invalid
            };
        }

        var ip = parts[0];
        var isValidIp = ValidateIpAddress(ip);
        var domains = parts.Skip(1).ToList();

        var rules = new List<HostRule>();

        for (var i = 0; i < domains.Count; i++)
        {
            var rule = new HostRule
            {
                IP = ip,
                Domain = domains[i],
                Comment = i == 0 ? comment : string.Empty,
                IsEnabled = !isDisabled,
                Status = !isDisabled && isValidIp ? RuleStatus.Active : RuleStatus.Inactive
            };

            if (!isValidIp)
            {
                rule.Status = RuleStatus.Invalid;
            }
            else if (isDisabled)
            {
                rule.Status = RuleStatus.Inactive;
            }

            rules.Add(rule);
        }

        if (rules.Count == 0)
        {
            return null;
        }

        if (rules.Count == 1)
        {
            return rules[0];
        }

        rules[0].Comment = comment;
        return rules[0];
    }

    private static bool IsLikelyHostEntry(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var firstToken = Regex.Split(line.Trim(), @"[\s\t]+")[0];
        return ValidateIpAddress(firstToken);
    }

    private static int FindCommentIndex(string line)
    {
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (line[i] == '#' && !inQuotes)
            {
                var hasSpaceBefore = i == 0 || char.IsWhiteSpace(line[i - 1]);
                if (hasSpaceBefore || i == 0)
                {
                    return i;
                }
            }
        }
        return -1;
    }

    private static bool ValidateIpAddress(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return false;
        }

        if (IPAddress.TryParse(ip, out var address))
        {
            return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ||
                   address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
        }

        return false;
    }

    public async Task WriteSystemHostsAsync(List<HostRule> rules, bool backupFirst = true)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var hostsPath = GetHostsFilePath();
        _logger.LogInformation("开始写入 hosts 文件: {Path}, 规则数: {Count}, 备份: {Backup}",
            hostsPath, rules.Count, backupFirst);
        _logService.LogAction("写入Hosts", $"开始写入 {rules.Count} 条规则到 hosts 文件", hostsPath);

        try
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);

            if (backupFirst && File.Exists(hostsPath))
            {
                await CreateBackupAsync(hostsPath).ConfigureAwait(false);
            }

            using var mutex = new Mutex(false, FileLockMutexName);
            var mutexAcquired = false;
            try
            {
                mutexAcquired = mutex.WaitOne(10000);
                if (!mutexAcquired)
                {
                    throw new TimeoutException("获取 hosts 文件写入互斥锁超时，请稍后重试");
                }

                var directory = Path.GetDirectoryName(hostsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var tempPath = hostsPath + ".tmp";
                var sb = new StringBuilder();

                sb.AppendLine("# =============================================================");
                sb.AppendLine("# Hosts file managed by HostManage");
                sb.AppendLine($"# Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine("# =============================================================");
                sb.AppendLine();
                sb.AppendLine("127.0.0.1\tlocalhost");
                sb.AppendLine("::1\t\tlocalhost");
                sb.AppendLine();

                var groupedRules = rules
                    .GroupBy(r => new { r.IP, r.IsEnabled, r.Comment })
                    .ToList();

                foreach (var group in groupedRules)
                {
                    var domains = group.Select(g => g.Domain).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    var firstRule = group.First();

                    if (domains.Count == 0)
                    {
                        continue;
                    }

                    var line = BuildHostLine(firstRule, domains);
                    sb.AppendLine(line);
                }

                if (_fileWatcher != null)
                    _fileWatcher.EnableRaisingEvents = false;

                try
                {
                    await File.WriteAllTextAsync(tempPath, sb.ToString(), new UTF8Encoding(true)).ConfigureAwait(false);

                    if (File.Exists(hostsPath))
                    {
                        File.SetAttributes(hostsPath, File.GetAttributes(hostsPath) & ~FileAttributes.ReadOnly);
                    }

                    File.Copy(tempPath, hostsPath, true);
                    File.Delete(tempPath);
                }
                finally
                {
                    if (_fileWatcher != null)
                        _fileWatcher.EnableRaisingEvents = true;
                }
            }
            finally
            {
                if (mutexAcquired)
                {
                    mutex.ReleaseMutex();
                }
            }

            _logger.LogInformation("成功写入 hosts 文件，共 {Count} 条规则", rules.Count);
            _logService.LogAction("写入Hosts", $"成功写入 {rules.Count} 条规则到 hosts 文件");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "无权限写入 hosts 文件");
            _logService.LogError("写入Hosts", "无权限写入 hosts 文件，请以管理员身份运行", ex.Message);
            throw;
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "写入 hosts 文件超时");
            _logService.LogError("写入Hosts", "写入 hosts 文件失败", ex.Message);
            throw;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "写入 hosts 文件时发生 IO 错误");
            _logService.LogError("写入Hosts", "写入 hosts 文件失败", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "写入 hosts 文件时发生未预期的错误");
            _logService.LogError("写入Hosts", "写入 hosts 文件时发生错误", ex.Message);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static string BuildHostLine(HostRule rule, List<string> domains)
    {
        var sb = new StringBuilder();

        if (!rule.IsEnabled)
        {
            sb.Append("# ");
        }

        sb.Append(rule.IP);

        var tabCount = rule.IP.Length < 16 ? 2 : 1;
        sb.Append(new string('\t', tabCount));

        sb.Append(string.Join(' ', domains));

        if (!string.IsNullOrWhiteSpace(rule.Comment))
        {
            sb.Append(" # ");
            sb.Append(rule.Comment.Trim());
        }

        return sb.ToString().TrimEnd();
    }

    private async Task CreateBackupAsync(string hostsPath)
    {
        try
        {
            var backupDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HostManage", "Backups");

            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            var backupFile = Path.Combine(backupDir,
                $"hosts_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.bak");

            var content = await File.ReadAllTextAsync(hostsPath, Encoding.UTF8).ConfigureAwait(false);
            await File.WriteAllTextAsync(backupFile, content, new UTF8Encoding(true)).ConfigureAwait(false);

            _logger.LogInformation("创建 hosts 文件备份: {BackupPath}", backupFile);
            _logService.LogInfo("备份Hosts", "创建 hosts 文件备份", backupFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "创建 hosts 文件备份失败");
            _logService.LogWarn("备份Hosts", "创建 hosts 文件备份失败", ex.Message);
        }
    }

    public async Task<bool> RequestAdminPrivilegesAsync()
    {
        _logger.LogInformation("请求管理员权限");

        if (await IsAdminAsync().ConfigureAwait(false))
        {
            _logger.LogInformation("当前进程已具有管理员权限");
            return true;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath ??
                               Path.ChangeExtension(Environment.GetCommandLineArgs()[0], ".exe"),
                    UseShellExecute = true,
                    Verb = "runas",
                    Arguments = string.Join(' ', Environment.GetCommandLineArgs().Skip(1))
                });

                if (process != null)
                {
                    _logger.LogInformation("已启动提权进程，PID: {Pid}", process.Id);
                    _logService.LogAction("提权", "已请求管理员权限，正在重启程序");
                    process.WaitForExit(2000);

                    ApplicationExitSafe();
                    return true;
                }
            }
            else
            {
                var script = BuildLinuxPkexecScript();
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "pkexec",
                    Arguments = script,
                    UseShellExecute = true
                });

                if (process != null)
                {
                    _logger.LogInformation("已启动 pkexec 提权进程");
                    _logService.LogAction("提权", "已请求管理员权限");
                    process.WaitForExit(2000);

                    ApplicationExitSafe();
                    return true;
                }
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            _logger.LogWarning("用户取消了管理员权限请求");
            _logService.LogWarn("提权", "用户取消了管理员权限请求");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "请求管理员权限时发生错误");
            _logService.LogError("提权", "请求管理员权限失败", ex.Message);
            return false;
        }

        return false;
    }

    private static string BuildLinuxPkexecScript()
    {
        var exePath = Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0];
        return $"\"{exePath}\" {string.Join(' ', Environment.GetCommandLineArgs().Skip(1))}";
    }

    private static void ApplicationExitSafe()
    {
        try
        {
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.Application.Current.Shutdown();
                });
            }
            else
            {
                Environment.Exit(0);
            }
        }
        catch
        {
            Environment.Exit(0);
        }
    }

    public Task<bool> IsAdminAsync()
    {
        return Task.FromResult(IsAdmin());
    }

    public bool IsAdmin()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

            var uid = GetUnixUserId();
            return uid == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "检查管理员权限时发生错误，返回 false");
            return false;
        }
    }

    private static int GetUnixUserId()
    {
        try
        {
            var idString = Environment.GetEnvironmentVariable("UID");
            if (!string.IsNullOrEmpty(idString) && int.TryParse(idString, out var uid))
            {
                return uid;
            }

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "id",
                Arguments = "-u",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);

                if (int.TryParse(output.Trim(), out var parsedUid))
                {
                    return parsedUid;
                }
            }
        }
        catch
        {
        }

        return -1;
    }

    public IDisposable AcquireFileLock()
    {
        return new HostFileLock(GetHostsFilePath(), _logger);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Changed -= OnHostsFileChanged;
                _fileWatcher.Created -= OnHostsFileChanged;
                _fileWatcher.Deleted -= OnHostsFileChanged;
                _fileWatcher.Renamed -= OnHostsFileChanged;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            _semaphore.Dispose();
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private sealed class HostFileLock : IDisposable
    {
        private readonly string _filePath;
        private readonly ILogger _logger;
        private FileStream? _fileStream;
        private Mutex? _mutex;
        private bool _disposed;

        public HostFileLock(string filePath, ILogger logger)
        {
            _filePath = filePath;
            _logger = logger;
            AcquireLock();
        }

        private void AcquireLock()
        {
            var mutexName = $"Global\\HostManage_FileLock_{Path.GetFileName(_filePath)}_{Math.Abs(_filePath.GetHashCode()):X}";
            _mutex = new Mutex(false, mutexName);

            _logger.LogDebug("尝试获取文件锁: {Path}", _filePath);

            var mutexAcquired = false;
            try
            {
                mutexAcquired = _mutex.WaitOne(10000);
                if (!mutexAcquired)
                {
                    throw new TimeoutException($"获取文件互斥锁超时: {_filePath}");
                }

                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _fileStream = new FileStream(
                    _filePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    4096,
                    FileOptions.None);

                _logger.LogDebug("成功获取文件锁: {Path}", _filePath);
            }
            catch
            {
                if (mutexAcquired && _mutex != null)
                {
                    _mutex.ReleaseMutex();
                }
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _fileStream?.Dispose();
                _fileStream = null;

                try
                {
                    _mutex?.ReleaseMutex();
                }
                catch (ObjectDisposedException)
                {
                }
                catch (ApplicationException ex)
                {
                    _logger.LogWarning(ex, "释放文件互斥锁时发生异常");
                }

                _mutex?.Dispose();
                _mutex = null;

                _logger.LogDebug("已释放文件锁: {Path}", _filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "释放文件锁时发生异常: {Path}", _filePath);
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
