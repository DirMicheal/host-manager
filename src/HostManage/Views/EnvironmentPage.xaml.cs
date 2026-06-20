using System.Windows;
using System.Windows.Controls;
using HostManage.Models;

namespace HostManage.Views;

public partial class EnvironmentPage : Page
{
    public EnvironmentPage()
    {
        InitializeComponent();
    }

    public bool? ShowSwitchConfirmDialog(HostEnvironment environment)
    {
        var result = MessageBox.Show(
            $"确定要切换到环境 \"{environment.Name}\" 吗？\n此操作将修改系统 hosts 文件。",
            "切换环境确认",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        return result == MessageBoxResult.OK;
    }

    public bool? ShowDeleteConfirmDialog(HostEnvironment environment)
    {
        var result = MessageBox.Show(
            $"确定要删除环境 \"{environment.Name}\" 吗？\n此操作不可撤销。",
            "删除环境确认",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        return result == MessageBoxResult.OK;
    }
}
