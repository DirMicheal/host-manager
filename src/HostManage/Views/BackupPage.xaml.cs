using System.Windows;
using System.Windows.Controls;
using HostManage.Models;

namespace HostManage.Views;

public partial class BackupPage : Page
{
    public BackupPage()
    {
        InitializeComponent();
    }

    public bool? ShowRestoreConfirmDialog(BackupSnapshot snapshot)
    {
        var result = MessageBox.Show(
            $"确定要恢复备份 \"{snapshot.Name}\" 吗？\n创建时间: {snapshot.Timestamp:yyyy-MM-dd HH:mm:ss}\n\n此操作将覆盖当前环境和hosts文件。",
            "恢复备份确认",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        return result == MessageBoxResult.OK;
    }

    public bool? ShowDeleteConfirmDialog(int count)
    {
        var result = MessageBox.Show(
            $"确定要删除选中的 {count} 个备份吗？\n此操作不可撤销。",
            "删除备份确认",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        return result == MessageBoxResult.OK;
    }
}
