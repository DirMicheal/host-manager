namespace HostManage.Services;

public interface INetworkService
{
    Task<PingResult> PingAsync(string ip, int timeoutMs = 3000);

    Task<List<PingResult>> PingBatchAsync(IEnumerable<string> ips, int timeoutMs = 3000, IProgress<int>? progress = null);

    Task<PortResult> TestPortAsync(string ip, int port, int timeoutMs = 3000);

    Task<List<PortResult>> TestPortBatchAsync(IEnumerable<(string ip, int port)> targets, int timeoutMs = 3000, IProgress<int>? progress = null);
}
