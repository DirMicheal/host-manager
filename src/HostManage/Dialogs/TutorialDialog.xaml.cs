using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HostManage.Models;
using HostManage.Services;
using MaterialDesignThemes.Wpf;

namespace HostManage.Dialogs;

public partial class TutorialDialog : Window
{
    private readonly ISettingsService? _settingsService;
    private int _currentStep = 1;
    private const int TotalSteps = 8;

    private readonly (PackIconKind Icon, string Title, string Description, string Tip)[] _steps = new[]
    {
        (PackIconKind.HandWave,
            "欢迎使用Host管理器",
            "欢迎使用Host管理器！这是一款专业的Host文件管理工具，帮助您轻松管理本地域名解析配置。支持多环境切换、批量操作、自动备份等高级功能，让您的开发测试工作更加高效便捷。接下来的几步将带您了解本软件的主要功能。",
            "小提示：您可以随时通过菜单栏中的「帮助 → 使用教程」重新打开本教程。"),

        (PackIconKind.PencilBoxMultiple,
            "基础操作 - 管理Host规则",
            "在主界面的「规则管理」页面，您可以轻松地添加、编辑和删除Host规则。点击「添加规则」按钮创建新规则，双击现有规则进行编辑，或选择规则后点击「删除」按钮移除。每条规则包含IP地址、域名、备注和启用状态，支持实时语法校验。",
            "小提示：支持IPv4和IPv6地址，域名一行一个，可使用通配符如 *.example.com。"),

        (PackIconKind.Layers,
            "多环境管理",
            "软件支持创建多个Host环境，例如「开发环境」「测试环境」「生产环境」等。在「环境管理」页面可以创建新环境、编辑现有环境、切换当前环境。使用「对比」功能可以快速查看两个环境之间的差异，方便排查问题。",
            "小提示：切换环境时会自动备份当前Host文件，确保可以随时恢复。"),

        (PackIconKind.SelectMultiple,
            "批量操作",
            "使用Ctrl或Shift键配合鼠标点击可以选择多条规则，然后进行批量操作：批量启用/停用、批量删除、批量修改所属环境。同时支持导入和导出功能，可以将规则导出为JSON或直接导入标准hosts文件格式。",
            "小提示：拖拽文件到主窗口可以快速导入hosts规则。"),

        (PackIconKind.ServerNetwork,
            "代理配置",
            "在「代理配置」页面可以配置系统代理或应用内代理。支持HTTP、HTTPS和SOCKS5三种代理协议，可分别设置地址、端口、用户名和密码。代理配置可以保存为多个方案，一键切换。",
            "小提示：配置代理后可进行连通性测试，确保代理可用。"),

        (PackIconKind.BackupRestore,
            "备份与恢复",
            "软件提供完善的备份恢复机制。每次应用环境前会自动创建快照，可在「备份管理」页面查看所有历史备份。支持一键恢复到任意历史版本，也可以手动创建备份点，确保数据安全。",
            "小提示：可在设置中配置自动备份数量上限，避免占用过多磁盘空间。"),

        (PackIconKind.SearchWeb,
            "系统检测工具",
            "内置的系统检测功能可以帮您扫描hosts文件中的潜在问题：检测IP冲突、重复域名、语法错误等。连通性检测可以验证指定IP的端口是否可达，支持Ping测试和TCP端口扫描。",
            "小提示：建议定期运行冲突扫描，确保Host文件配置正确无误。"),

        (PackIconKind.CheckCircleOutline,
            "恭喜！教程已完成",
            "您已完成Host管理器的全部教程内容！现在您可以开始使用了。如果在使用过程中有任何问题，随时可以通过帮助菜单重新打开本教程，或查看在线文档。祝您使用愉快！",
            "小贴士：勾选左侧的「启动时不再显示此教程」可跳过下次启动时的教程展示。")
    };

    public TutorialDialog()
    {
        InitializeComponent();
        DataContext = this;
        UpdateStepContent();
    }

    public TutorialDialog(ISettingsService settingsService) : this()
    {
        _settingsService = settingsService;
    }

    private void UpdateStepContent()
    {
        var step = _steps[_currentStep - 1];
        StepIcon.Kind = step.Icon;
        StepTitle.Text = step.Title;
        StepDescription.Text = step.Description;
        StepTip.Text = step.Tip;

        PrevButton.IsEnabled = _currentStep > 1;
        if (_currentStep == TotalSteps)
        {
            NextButton.Content = "开始使用";
            SkipButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            NextButton.Content = "下一步";
            SkipButton.Visibility = Visibility.Visible;
        }

        UpdateStepIndicators();
    }

    private void UpdateStepIndicators()
    {
        var indicators = new[]
        {
            StepIndicator1, StepIndicator2, StepIndicator3, StepIndicator4,
            StepIndicator5, StepIndicator6, StepIndicator7, StepIndicator8
        };

        var primaryBrush = (Brush)FindResource("PrimaryHueMidBrush");
        var grayBrush = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
        var lightBrush = (Brush)FindResource("PrimaryHueLightBrush");

        var filledKinds = new[]
        {
            PackIconKind.Numeric1Circle, PackIconKind.Numeric2Circle,
            PackIconKind.Numeric3Circle, PackIconKind.Numeric4Circle,
            PackIconKind.Numeric5Circle, PackIconKind.Numeric6Circle,
            PackIconKind.Numeric7Circle, PackIconKind.Numeric8Circle
        };

        var outlineKinds = new[]
        {
            PackIconKind.Numeric1CircleOutline, PackIconKind.Numeric2CircleOutline,
            PackIconKind.Numeric3CircleOutline, PackIconKind.Numeric4CircleOutline,
            PackIconKind.Numeric5CircleOutline, PackIconKind.Numeric6CircleOutline,
            PackIconKind.Numeric7CircleOutline, PackIconKind.Numeric8CircleOutline
        };

        var textBlocks = new[] { "欢迎", "基础操作", "多环境", "批量操作", "代理配置", "备份恢复", "系统检测", "完成" };

        for (int i = 0; i < indicators.Length; i++)
        {
            if (i + 1 < _currentStep)
            {
                indicators[i].Kind = PackIconKind.CheckCircle;
                indicators[i].Foreground = lightBrush;
                if (indicators[i].Parent is StackPanel panel && panel.Children.Count > 1 && panel.Children[1] is TextBlock tb)
                {
                    tb.Foreground = lightBrush;
                }
            }
            else if (i + 1 == _currentStep)
            {
                indicators[i].Kind = filledKinds[i];
                indicators[i].Foreground = primaryBrush;
                if (indicators[i].Parent is StackPanel panel && panel.Children.Count > 1 && panel.Children[1] is TextBlock tb)
                {
                    tb.Foreground = primaryBrush;
                    tb.FontWeight = FontWeights.SemiBold;
                }
            }
            else
            {
                indicators[i].Kind = outlineKinds[i];
                indicators[i].Foreground = grayBrush;
                if (indicators[i].Parent is StackPanel panel && panel.Children.Count > 1 && panel.Children[1] is TextBlock tb)
                {
                    tb.Foreground = grayBrush;
                    tb.FontWeight = FontWeights.Normal;
                }
            }
        }

        var textParents = new[]
        {
            StepIndicator1.Parent, StepIndicator2.Parent, StepIndicator3.Parent, StepIndicator4.Parent,
            StepIndicator5.Parent, StepIndicator6.Parent, StepIndicator7.Parent, StepIndicator8.Parent
        };
    }

    private void PrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep > 1)
        {
            _currentStep--;
            UpdateStepContent();
        }
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep < TotalSteps)
        {
            _currentStep++;
            UpdateStepContent();
        }
        else
        {
            await SaveSettingsAndClose();
        }
    }

    private async void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveSettingsAndClose();
    }

    private async System.Threading.Tasks.Task SaveSettingsAndClose()
    {
        if (_settingsService != null && DontShowAgainCheckBox.IsChecked == true)
        {
            _settingsService.Current.ShowTutorialOnStartup = false;
            await _settingsService.SaveSettingsAsync();
        }

        DialogResult = true;
        Close();
    }
}
