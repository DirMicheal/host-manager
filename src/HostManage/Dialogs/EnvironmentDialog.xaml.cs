using System.Windows;
using HostManage.Models;

namespace HostManage.Dialogs;

public partial class EnvironmentDialog : Window
{
    public string DialogTitle { get; set; } = "新建环境";

    public string EnvironmentName { get; set; } = string.Empty;

    public string EnvironmentDescription { get; set; } = string.Empty;

    public HostEnvironment? Result { get; private set; }

    public EnvironmentDialog()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) => NameTextBox.Focus();
    }

    public EnvironmentDialog(HostEnvironment environment) : this()
    {
        DialogTitle = "编辑环境";
        EnvironmentName = environment.Name;
        EnvironmentDescription = environment.Description;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EnvironmentName))
        {
            MessageBox.Show("环境名称不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }

        Result = new HostEnvironment
        {
            Name = EnvironmentName.Trim(),
            Description = EnvironmentDescription?.Trim() ?? string.Empty,
            UpdatedAt = DateTime.Now
        };

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
