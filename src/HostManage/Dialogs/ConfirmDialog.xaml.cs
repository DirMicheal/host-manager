using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace HostManage.Dialogs;

public enum ConfirmIconType
{
    Warning,
    Error,
    Info,
    Question
}

public enum ConfirmButtons
{
    YesNo,
    OkCancel
}

public partial class ConfirmDialog : Window
{
    public ConfirmIconType IconType { get; set; } = ConfirmIconType.Question;
    public ConfirmButtons Buttons { get; set; } = ConfirmButtons.OkCancel;
    public new string Title { get; set; } = "确认";
    public string Message { get; set; } = "确定要执行此操作吗？";
    public new bool? DialogResult { get; private set; }

    public string YesButtonText { get; set; } = "是";
    public string NoButtonText { get; set; } = "否";
    public string OkButtonText { get; set; } = "确定";
    public string CancelButtonText { get; set; } = "取消";

    public ConfirmDialog()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += OnLoaded;
    }

    public ConfirmDialog(string title, string message,
        ConfirmIconType iconType = ConfirmIconType.Question,
        ConfirmButtons buttons = ConfirmButtons.OkCancel) : this()
    {
        Title = title;
        Message = message;
        IconType = iconType;
        Buttons = buttons;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        base.Title = Title;
        TitleText.Text = Title;
        MessageText.Text = Message;
        ConfigureIcon();
        ConfigureButtons();
    }

    private void ConfigureIcon()
    {
        switch (IconType)
        {
            case ConfirmIconType.Warning:
                DialogIcon.Kind = PackIconKind.Alert;
                DialogIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
                IconBorder.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xE1));
                break;

            case ConfirmIconType.Error:
                DialogIcon.Kind = PackIconKind.AlertCircle;
                DialogIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
                IconBorder.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0xEE));
                break;

            case ConfirmIconType.Info:
                DialogIcon.Kind = PackIconKind.Information;
                DialogIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3));
                IconBorder.Background = new SolidColorBrush(Color.FromRgb(0xE3, 0xF2, 0xFD));
                break;

            case ConfirmIconType.Question:
            default:
                DialogIcon.Kind = PackIconKind.HelpCircle;
                DialogIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2));
                IconBorder.Background = new SolidColorBrush(Color.FromRgb(0xE3, 0xF2, 0xFD));
                break;
        }
    }

    private void ConfigureButtons()
    {
        switch (Buttons)
        {
            case ConfirmButtons.YesNo:
                CancelButton.Content = NoButtonText;
                ConfirmButton.Content = YesButtonText;
                break;

            case ConfirmButtons.OkCancel:
            default:
                CancelButton.Content = CancelButtonText;
                ConfirmButton.Content = OkButtonText;
                break;
        }
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public static bool? Show(string title, string message,
        ConfirmIconType iconType = ConfirmIconType.Question,
        ConfirmButtons buttons = ConfirmButtons.OkCancel,
        Window? owner = null)
    {
        var dialog = new ConfirmDialog(title, message, iconType, buttons);
        if (owner != null)
        {
            dialog.Owner = owner;
        }
        dialog.ShowDialog();
        return dialog.DialogResult;
    }

    public static bool? ShowWarning(string title, string message,
        ConfirmButtons buttons = ConfirmButtons.OkCancel,
        Window? owner = null)
    {
        return Show(title, message, ConfirmIconType.Warning, buttons, owner);
    }

    public static bool? ShowError(string title, string message,
        ConfirmButtons buttons = ConfirmButtons.OkCancel,
        Window? owner = null)
    {
        return Show(title, message, ConfirmIconType.Error, buttons, owner);
    }

    public static bool? ShowInfo(string title, string message,
        ConfirmButtons buttons = ConfirmButtons.OkCancel,
        Window? owner = null)
    {
        return Show(title, message, ConfirmIconType.Info, buttons, owner);
    }

    public static bool? ShowQuestion(string title, string message,
        ConfirmButtons buttons = ConfirmButtons.YesNo,
        Window? owner = null)
    {
        return Show(title, message, ConfirmIconType.Question, buttons, owner);
    }
}
