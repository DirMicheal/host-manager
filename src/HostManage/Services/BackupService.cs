using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using HostManage.Models;
using Newtonsoft.Json;

namespace HostManage.Services;

public class BackupService : IBackupService
{
    private readonly IHostFileService _hostFileService;
    private readonly ILogService _logService;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly string _dataDirectory;
    private readonly string _backupsDirectory;
    private readonly string _snapshotsFilePath;

    public ObservableCollection<BackupSnapshot> Snapshots { get; private set; } = new();

    public BackupService(IHostFileService hostFileService, ILogService logService)
    {
        _hostFileService = hostFileService;
        _logService = logService;

        _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HostManage");
        _backupsDirectory = Path.Combine(_dataDirectory, "Backups");
        _snapshotsFilePath = Path.Combine(_dataDirectory, "backups.json");
    }

    public async Task<BackupSnapshot> CreateBackupAsync(string? name = null, bool isManual = true)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            EnsureDirectories();
            await LoadSnapshotsInternalAsync().ConfigureAwait(false);

            var hostsFilePath = _hostFileService.GetHostsFilePath();
            if (!File.Exists(hostsFilePath))
                throw new FileNotFoundException("未找到系统hosts文件", hostsFilePath);

            var timestamp = DateTime.Now;
            var timestampStr = timestamp.ToString("yyyyMMdd_HHmmss_fff");
            var backupFileName = $"backup_{timestampStr}.bak";
            var backupFilePath = Path.Combine(_backupsDirectory, backupFileName);

            using (var fileLock = _hostFileService.AcquireFileLock())
            {
                File.Copy(hostsFilePath, backupFilePath, true);
            }

            var content = await File.ReadAllBytesAsync(backupFilePath).ConfigureAwait(false);
            var contentHash = ComputeSha256Hash(content);
            var size = content.Length;

            var snapshot = new BackupSnapshot
            {
                Name = string.IsNullOrWhiteSpace(name) ? $"备份_{timestampStr}" : name,
                Timestamp = timestamp,
                ContentHash = contentHash,
                FilePath = backupFilePath,
                Size = size,
                IsManual = isManual,
                Description = isManual ? "手动备份" : "自动备份"
            };

            Snapshots.Add(snapshot);
            await SaveSnapshotsInternalAsync().ConfigureAwait(false);

            _logService.LogAction("创建备份", $"成功创建备份 \"{snapshot.Name}\"", $"文件: {backupFileName}, 大小: {FormatSize(size)}");

            return snapshot;
        }
        catch (Exception ex)
        {
            _logService.LogError("创建备份", $"创建备份失败: {ex.Message}", ex.ToString());
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RestoreBackupAsync(Guid snapshotId)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var snapshot = Snapshots.FirstOrDefault(s => s.Id == snapshotId);
            if (snapshot == null)
                throw new ArgumentException($"未找到ID为 {snapshotId} 的备份快照", nameof(snapshotId));

            if (!File.Exists(snapshot.FilePath))
                throw new FileNotFoundException($"备份文件不存在: {snapshot.FilePath}", snapshot.FilePath);

            var hostsFilePath = _hostFileService.GetHostsFilePath();

            using (var fileLock = _hostFileService.AcquireFileLock())
            {
                if (File.Exists(hostsFilePath))
                {
                    var currentContent = await File.ReadAllBytesAsync(hostsFilePath).ConfigureAwait(false);
                    var currentHash = ComputeSha256Hash(currentContent);
                    if (!currentHash.Equals(snapshot.ContentHash, StringComparison.OrdinalIgnoreCase))
                    {
                        var preRestoreBackup = await CreateBackupAsync("恢复前自动备份", false).ConfigureAwait(false);
                        _logService.LogInfo("恢复备份", $"已在恢复前创建备份: {preRestoreBackup.Name}");
                    }
                }

                File.Copy(snapshot.FilePath, hostsFilePath, true);
            }

            _logService.LogAction("恢复备份", $"成功恢复备份 \"{snapshot.Name}\"", $"文件: {Path.GetFileName(snapshot.FilePath)}");
        }
        catch (Exception ex)
        {
            _logService.LogError("恢复备份", $"恢复备份失败: {ex.Message}", ex.ToString());
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteBackupAsync(Guid snapshotId)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var snapshot = Snapshots.FirstOrDefault(s => s.Id == snapshotId);
            if (snapshot == null)
                throw new ArgumentException($"未找到ID为 {snapshotId} 的备份快照", nameof(snapshotId));

            var name = snapshot.Name;
            var filePath = snapshot.FilePath;

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            Snapshots.Remove(snapshot);
            await SaveSnapshotsInternalAsync().ConfigureAwait(false);

            _logService.LogAction("删除备份", $"成功删除备份 \"{name}\"");
        }
        catch (Exception ex)
        {
            _logService.LogError("删除备份", $"删除备份失败: {ex.Message}", ex.ToString());
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<int> CleanupOldBackupsAsync(int maxCount = 30)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            EnsureDirectories();
            await LoadSnapshotsInternalAsync().ConfigureAwait(false);

            if (maxCount <= 0)
                throw new ArgumentException("最大保留数量必须大于0", nameof(maxCount));

            var removedCount = 0;
            var sortedSnapshots = Snapshots.OrderByDescending(s => s.Timestamp).ToList();

            if (sortedSnapshots.Count <= maxCount)
                return 0;

            var toRemove = sortedSnapshots.Skip(maxCount).ToList();

            foreach (var snapshot in toRemove)
            {
                try
                {
                    if (File.Exists(snapshot.FilePath))
                    {
                        File.Delete(snapshot.FilePath);
                    }

                    Snapshots.Remove(snapshot);
                    removedCount++;
                }
                catch (Exception ex)
                {
                    _logService.LogWarn("清理备份", $"删除备份 \"{snapshot.Name}\" 时出错: {ex.Message}");
                }
            }

            if (removedCount > 0)
            {
                await SaveSnapshotsInternalAsync().ConfigureAwait(false);
                _logService.LogAction("清理备份", $"成功清理 {removedCount} 个旧备份，保留最近 {maxCount} 个");
            }

            return removedCount;
        }
        catch (Exception ex)
        {
            _logService.LogError("清理备份", $"清理旧备份失败: {ex.Message}", ex.ToString());
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<string> ExportBackupAsync(Guid snapshotId, string targetPath)
    {
        try
        {
            var snapshot = Snapshots.FirstOrDefault(s => s.Id == snapshotId);
            if (snapshot == null)
                throw new ArgumentException($"未找到ID为 {snapshotId} 的备份快照", nameof(snapshotId));

            if (!File.Exists(snapshot.FilePath))
                throw new FileNotFoundException($"备份文件不存在: {snapshot.FilePath}", snapshot.FilePath);

            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            if (Directory.Exists(targetPath))
            {
                var fileName = Path.GetFileName(snapshot.FilePath);
                targetPath = Path.Combine(targetPath, fileName);
            }

            using var sourceStream = new FileStream(snapshot.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var targetStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await sourceStream.CopyToAsync(targetStream).ConfigureAwait(false);

            _logService.LogAction("导出备份", $"成功导出备份 \"{snapshot.Name}\" 到 \"{targetPath}\"");

            return targetPath;
        }
        catch (Exception ex)
        {
            _logService.LogError("导出备份", $"导出备份失败: {ex.Message}", ex.ToString());
            throw;
        }
    }

    public async Task ImportBackupAsync(string sourcePath)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException($"源文件不存在: {sourcePath}", sourcePath);

            EnsureDirectories();
            await LoadSnapshotsInternalAsync().ConfigureAwait(false);

            var timestamp = DateTime.Now;
            var timestampStr = timestamp.ToString("yyyyMMdd_HHmmss_fff");
            var originalName = Path.GetFileNameWithoutExtension(sourcePath);
            var backupFileName = $"import_{originalName}_{timestampStr}.bak";
            var backupFilePath = Path.Combine(_backupsDirectory, backupFileName);

            File.Copy(sourcePath, backupFilePath, true);

            var content = await File.ReadAllBytesAsync(backupFilePath).ConfigureAwait(false);
            var contentHash = ComputeSha256Hash(content);
            var size = content.Length;

            var snapshot = new BackupSnapshot
            {
                Name = $"导入_{originalName}",
                Timestamp = timestamp,
                ContentHash = contentHash,
                FilePath = backupFilePath,
                Size = size,
                IsManual = true,
                Description = $"从文件导入: {sourcePath}"
            };

            Snapshots.Add(snapshot);
            await SaveSnapshotsInternalAsync().ConfigureAwait(false);

            _logService.LogAction("导入备份", $"成功导入备份 \"{snapshot.Name}\"", $"源文件: {sourcePath}");
        }
        catch (Exception ex)
        {
            _logService.LogError("导入备份", $"导入备份失败: {ex.Message}", ex.ToString());
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void EnsureDirectories()
    {
        if (!Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
        }

        if (!Directory.Exists(_backupsDirectory))
        {
            Directory.CreateDirectory(_backupsDirectory);
        }
    }

    private async Task LoadSnapshotsInternalAsync()
    {
        if (Snapshots.Count > 0)
            return;

        try
        {
            if (File.Exists(_snapshotsFilePath))
            {
                var json = await File.ReadAllTextAsync(_snapshotsFilePath).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var list = JsonConvert.DeserializeObject<List<BackupSnapshot>>(json);
                    if (list != null)
                    {
                        var validSnapshots = new List<BackupSnapshot>();
                        foreach (var snapshot in list)
                        {
                            if (File.Exists(snapshot.FilePath))
                            {
                                validSnapshots.Add(snapshot);
                            }
                        }

                        Snapshots = new ObservableCollection<BackupSnapshot>(validSnapshots);

                        if (validSnapshots.Count != list.Count)
                        {
                            await SaveSnapshotsInternalAsync().ConfigureAwait(false);
                        }

                        return;
                    }
                }
            }

            Snapshots = new ObservableCollection<BackupSnapshot>();
        }
        catch (Exception ex)
        {
            _logService.LogError("加载备份", $"加载备份元数据失败: {ex.Message}", ex.ToString());
            Snapshots = new ObservableCollection<BackupSnapshot>();
        }
    }

    private async Task SaveSnapshotsInternalAsync()
    {
        try
        {
            EnsureDirectories();

            var list = Snapshots.OrderByDescending(s => s.Timestamp).ToList();
            var json = JsonConvert.SerializeObject(list, Formatting.Indented);
            await File.WriteAllTextAsync(_snapshotsFilePath, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logService.LogError("保存备份", $"保存备份元数据失败: {ex.Message}", ex.ToString());
            throw;
        }
    }

    private static string ComputeSha256Hash(byte[] content)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(content);
        var hash = BitConverter.ToString(bytes).Replace("-", string.Empty);
        return hash.ToLowerInvariant();
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
