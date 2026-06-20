namespace HostManage.Services;

public class PingResult
{
    public string IP { get; set; } = string.Empty;

    public bool Success { get; set; }

    public long RoundtripTime { get; set; }

    public string? ErrorMessage { get; set; }
}
