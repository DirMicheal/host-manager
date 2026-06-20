using System.Windows;
using HostManage.Services;

namespace HostManage.Dialogs;

public partial class CompareDialog : Window
{
    public string Subtitle { get; set; } = string.Empty;

    public int AddedCount { get; set; }

    public int RemovedCount { get; set; }

    public int ModifiedCount { get; set; }

    public IEnumerable<DiffItem>? DiffItems { get; set; }

    public CompareDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    public CompareDialog(string environmentName, IEnumerable<DiffItem> diffItems) : this()
    {
        Subtitle = $"环境: {environmentName} 与系统当前Hosts文件对比";
        DiffItems = diffItems.ToList();

        AddedCount = DiffItems.Count(d => d.Type == DiffType.Added);
        RemovedCount = DiffItems.Count(d => d.Type == DiffType.Removed);
        ModifiedCount = DiffItems.Count(d => d.Type == DiffType.Modified);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
