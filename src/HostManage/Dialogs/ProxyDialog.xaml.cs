using System.Net;
using System.Net.Sockets;
using System.Windows;
using HostManage.Models;

namespace HostManage.Dialogs;

public partial class ProxyDialog : Window
{
    private string _proxyName = string.Empty;
    private string _host = string.Empty;
    private string _port = string.Empty;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private ProxyType _selectedType = ProxyType.Http;

    public string DialogTitle { get; set; } = "新建代理";

    public string ProxyName
    {
        get => _proxyName;
        set => _proxyName = value;
    }

    public IEnumerable<ProxyType> ProxyTypes { get; } = new[] { ProxyType.Http, ProxyType.Https, ProxyType.Socks5 };

    public ProxyType SelectedType
    {
        get => _selectedType;
        set => _selectedType = value;
    }

    public string Host
    {
        get => _host;
        set => _host = value;
    }

    public string Port
    {
        get => _port;
        set => _port = value;
    }

    public string Username
    {
        get => _username;
        set => _username = value;
    }

    public ProxyConfig? Result { get; private set; }

    public ProxyDialog()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) => NameTextBox.Focus();
    }

    public ProxyDialog(ProxyConfig config) : this()
    {
        DialogTitle = "编辑代理";
        ProxyName = config.Name;
        SelectedType = config.Type;
        Host = config.Host;
        Port = config.Port.ToString();
        Username = config.Username;
        _password = config.Password;
        PasswordBox.Password = config.Password;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _password = PasswordBox.Password;
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateInput(out var message))
        {
            MessageBox.Show(message, "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var testButton = (System.Windows.Controls.Button)sender;
        testButton.IsEnabled = false;
        testButton.Content = "测试中...";

        try
        {
            var port = int.Parse(Port.Trim());
            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(Host.Trim(), port);
            var timeoutTask = Task.Delay(5000);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            if (completedTask == timeoutTask)
            {
                MessageBox.Show("连接超时，请检查地址和端口是否正确", "测试失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                await connectTask;
                if (tcpClient.Connected)
                {
                    MessageBox.Show("连接成功！代理地址可达。", "测试成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("连接失败，无法建立连接", "测试失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"测试异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            testButton.IsEnabled = true;
            testButton.Content = "测试连接";
        }
    }

    private bool ValidateInput(out string message)
    {
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(ProxyName))
        {
            message = "代理名称不能为空";
            return false;
        }

        if (SelectedType == ProxyType.None)
        {
            message = "请选择代理类型";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            message = "主机地址不能为空";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Port))
        {
            message = "端口不能为空";
            return false;
        }

        if (!int.TryParse(Port.Trim(), out var port) || port < 1 || port > 65535)
        {
            message = "端口必须是1-65535之间的整数";
            return false;
        }

        return true;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateInput(out var message))
        {
            MessageBox.Show(message, "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new ProxyConfig
        {
            Name = ProxyName.Trim(),
            Type = SelectedType,
            Host = Host.Trim(),
            Port = int.Parse(Port.Trim()),
            Username = Username?.Trim() ?? string.Empty,
            Password = _password ?? string.Empty
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
