using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace HostManage.Services;

public class NetworkService : INetworkService
{
    private const int MaxConcurrency = 10;

    private readonly ILogger<NetworkService> _logger;

    public NetworkService(ILogger<NetworkService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PingResult> PingAsync(string ip, int timeoutMs = 3000)
    {
        var result = new PingResult
        {
            IP = ip ?? string.Empty,
            Success = false,
            RoundtripTime = -1
        };

        try
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                result.ErrorMessage = "IP地址不能为空";
                return result;
            }

            string resolvedIp = ip.Trim();

            if (!IPAddress.TryParse(resolvedIp, out _))
            {
                try
                {
                    var hostEntry = await Dns.GetHostEntryAsync(resolvedIp);
                    if (hostEntry.AddressList.Length > 0)
                    {
                        resolvedIp = hostEntry.AddressList[0].ToString();
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorMessage = $"DNS解析失败: {ex.Message}";
                    _logger.LogWarning(ex, "DNS解析失败: {Host}", ip);
                    return result;
                }
            }

            using var ping = new Ping();
            var buffer = new byte[32];
            var options = new PingOptions(64, true);

            var reply = await ping.SendPingAsync(resolvedIp, timeoutMs, buffer, options);

            if (reply.Status == IPStatus.Success)
            {
                result.Success = true;
                result.RoundtripTime = reply.RoundtripTime;
            }
            else
            {
                result.ErrorMessage = $"Ping失败: {reply.Status}";
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Ping操作发生错误: {IP}", ip);
        }

        return result;
    }

    public async Task<List<PingResult>> PingBatchAsync(IEnumerable<string> ips, int timeoutMs = 3000, IProgress<int>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(ips);

        var ipList = ips.Where(ip => !string.IsNullOrWhiteSpace(ip)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var results = new List<PingResult>(ipList.Count);
        var completedCount = 0;
        var lockObj = new object();

        using var semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);

        var tasks = ipList.Select(async ip =>
        {
            await semaphore.WaitAsync();
            try
            {
                var result = await PingAsync(ip, timeoutMs);
                lock (lockObj)
                {
                    results.Add(result);
                    completedCount++;
                    progress?.Report(completedCount);
                }
                return result;
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        return results;
    }

    public async Task<PortResult> TestPortAsync(string ip, int port, int timeoutMs = 3000)
    {
        var result = new PortResult
        {
            IP = ip ?? string.Empty,
            Port = port,
            IsOpen = false
        };

        try
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                result.ErrorMessage = "IP地址不能为空";
                return result;
            }

            if (port < 1 || port > 65535)
            {
                result.ErrorMessage = "端口号必须在1-65535之间";
                return result;
            }

            string resolvedIp = ip.Trim();

            if (!IPAddress.TryParse(resolvedIp, out var address))
            {
                try
                {
                    var hostEntry = await Dns.GetHostEntryAsync(resolvedIp);
                    if (hostEntry.AddressList.Length > 0)
                    {
                        address = hostEntry.AddressList[0];
                        resolvedIp = address.ToString();
                        result.IP = resolvedIp;
                    }
                    else
                    {
                        result.ErrorMessage = "DNS解析未返回有效地址";
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorMessage = $"DNS解析失败: {ex.Message}";
                    _logger.LogWarning(ex, "DNS解析失败: {Host}", ip);
                    return result;
                }
            }

            using var client = new TcpClient(address.AddressFamily);
            using var connectTimeout = new CancellationTokenSource(timeoutMs);

            try
            {
                await client.ConnectAsync(address, port, connectTimeout.Token);
                result.IsOpen = true;
            }
            catch (OperationCanceledException)
            {
                result.ErrorMessage = $"连接超时（{timeoutMs}ms）";
            }
            catch (SocketException ex)
            {
                result.ErrorMessage = ex.Message;
                if (ex.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    result.ErrorMessage = "连接被拒绝";
                }
                else if (ex.SocketErrorCode == SocketError.HostUnreachable)
                {
                    result.ErrorMessage = "主机不可达";
                }
                else if (ex.SocketErrorCode == SocketError.NetworkUnreachable)
                {
                    result.ErrorMessage = "网络不可达";
                }
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "端口测试发生错误: {IP}:{Port}", ip, port);
        }

        return result;
    }

    public async Task<List<PortResult>> TestPortBatchAsync(IEnumerable<(string ip, int port)> targets, int timeoutMs = 3000, IProgress<int>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(targets);

        var targetList = targets
            .Where(t => !string.IsNullOrWhiteSpace(t.ip) && t.port >= 1 && t.port <= 65535)
            .Distinct()
            .ToList();

        var results = new List<PortResult>(targetList.Count);
        var completedCount = 0;
        var lockObj = new object();

        using var semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);

        var tasks = targetList.Select(async target =>
        {
            await semaphore.WaitAsync();
            try
            {
                var result = await TestPortAsync(target.ip, target.port, timeoutMs);
                lock (lockObj)
                {
                    results.Add(result);
                    completedCount++;
                    progress?.Report(completedCount);
                }
                return result;
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        return results;
    }
}
