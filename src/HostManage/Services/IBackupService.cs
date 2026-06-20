using System.Collections.ObjectModel;
using HostManage.Models;

namespace HostManage.Services;

public interface IBackupService
{
    ObservableCollection<BackupSnapshot> Snapshots { get; }

    Task<BackupSnapshot> CreateBackupAsync(string? name = null, bool isManual = true);

    Task RestoreBackupAsync(Guid snapshotId);

    Task DeleteBackupAsync(Guid snapshotId);

    Task<int> CleanupOldBackupsAsync(int maxCount = 30);

    Task<string> ExportBackupAsync(Guid snapshotId, string targetPath);

    Task ImportBackupAsync(string sourcePath);
}
