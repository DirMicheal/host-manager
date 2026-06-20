namespace HostManage.Services;

public class PortResult
{
    public string IP { get; set; } = string.Empty;

    public int Port { get; set; }

    public bool IsOpen { get; set; }

    public string? ErrorMessage { get; set; }
}
