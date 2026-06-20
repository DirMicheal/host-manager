using System.Text.RegularExpressions;
using System.Windows;
using HostManage.Models;

namespace HostManage.Dialogs;

public partial class RuleEditDialog : Window
{
    private bool _hasIpError;
    private bool _hasDomainError;
    private string _ip = string.Empty;
    private string _domain = string.Empty;
    private string _comment = string.Empty;
    private bool _isEnabled = true;
    private string _ipValidationMessage = string.Empty;
    private string _domainValidationMessage = string.Empty;

    public string DialogTitle { get; set; } = "编辑规则";

    public string IP
    {
        get => _ip;
        set
        {
            _ip = value;
            ValidateIP();
            UpdateSyntaxMessage();
        }
    }

    public string Domain
    {
        get => _domain;
        set
        {
            _domain = value;
            ValidateDomain();
            UpdateSyntaxMessage();
        }
    }

    public string Comment
    {
        get => _comment;
        set => _comment = value;
    }

    public bool RuleIsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    public bool IPHasError
    {
        get => _hasIpError;
        private set
        {
            _hasIpError = value;
            UpdateIsValid();
        }
    }

    public bool DomainHasError
    {
        get => _hasDomainError;
        private set
        {
            _hasDomainError = value;
            UpdateIsValid();
        }
    }

    public string IPValidationMessage
    {
        get => _ipValidationMessage;
        private set => _ipValidationMessage = value;
    }

    public string DomainValidationMessage
    {
        get => _domainValidationMessage;
        private set => _domainValidationMessage = value;
    }

    public string SyntaxMessage { get; private set; } = string.Empty;

    public bool IsValid { get; private set; }

    public HostRule? Result { get; private set; }

    public RuleEditDialog()
    {
        InitializeComponent();
        DataContext = this;
        UpdateSyntaxMessage();
    }

    public RuleEditDialog(HostRule rule) : this()
    {
        DialogTitle = "编辑规则";
        IP = rule.IP;
        Domain = rule.Domain;
        Comment = rule.Comment;
        RuleIsEnabled = rule.IsEnabled;
    }

    private void ValidateIP()
    {
        if (string.IsNullOrWhiteSpace(IP))
        {
            IPHasError = true;
            IPValidationMessage = "IP地址不能为空";
            return;
        }

        var ipv4Pattern = @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";
        var ipv6Pattern = @"^([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}$|^::$|^::1$|^::$";

        if (!Regex.IsMatch(IP.Trim(), ipv4Pattern) && !Regex.IsMatch(IP.Trim(), ipv6Pattern))
        {
            IPHasError = true;
            IPValidationMessage = "IP地址格式不正确";
            return;
        }

        IPHasError = false;
        IPValidationMessage = string.Empty;
    }

    private void ValidateDomain()
    {
        if (string.IsNullOrWhiteSpace(Domain))
        {
            DomainHasError = true;
            DomainValidationMessage = "域名不能为空";
            return;
        }

        var domainPattern = @"^(\*\.)?([a-zA-Z0-9]([a-zA-Z0-9\-]*[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$|^localhost$";
        var lines = Domain.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine)) continue;

            if (!Regex.IsMatch(trimmedLine, domainPattern))
            {
                DomainHasError = true;
                DomainValidationMessage = $"域名格式不正确: {trimmedLine}";
                return;
            }
        }

        DomainHasError = false;
        DomainValidationMessage = string.Empty;
    }

    private void UpdateSyntaxMessage()
    {
        if (IPHasError || DomainHasError)
        {
            SyntaxMessage = "请修正上方标记的错误后再保存。";
        }
        else if (!string.IsNullOrWhiteSpace(IP) && !string.IsNullOrWhiteSpace(Domain))
        {
            SyntaxMessage = "✓ 格式正确，规则将被正确解析。";
        }
        else
        {
            SyntaxMessage = "请输入IP地址和域名信息。";
        }
    }

    private void UpdateIsValid()
    {
        IsValid = !IPHasError && !DomainHasError && !string.IsNullOrWhiteSpace(IP) && !string.IsNullOrWhiteSpace(Domain);
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!IsValid) return;

        Result = new HostRule
        {
            IP = IP.Trim(),
            Domain = Domain.Trim(),
            Comment = Comment?.Trim() ?? string.Empty,
            IsEnabled = RuleIsEnabled,
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
