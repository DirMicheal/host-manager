namespace HostManage.Models;

public enum RuleStatus
{
    Active,
    Inactive,
    Conflict,
    Invalid
}

public enum EnvironmentType
{
    Development,
    Testing,
    Staging,
    Production,
    Custom
}

public enum ProxyType
{
    Http,
    Https,
    Socks5,
    None
}

public enum LogLevel
{
    Info,
    Warn,
    Error,
    Action
}

public enum ThemeType
{
    Light,
    Dark
}
