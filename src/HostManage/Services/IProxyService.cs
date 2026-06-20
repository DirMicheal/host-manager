using System.Collections.ObjectModel;
using HostManage.Models;

namespace HostManage.Services;

public interface IProxyService
{
    ObservableCollection<ProxyConfig> Proxies { get; }

    Task AddProxyAsync(ProxyConfig proxy);

    Task DeleteProxyAsync(Guid proxyId);

    Task UpdateProxyAsync(ProxyConfig proxy);

    Task ApplySystemProxyAsync(ProxyConfig? proxy);

    Task ClearSystemProxyAsync();

    Task<ProxyConfig?> GetCurrentSystemProxyAsync();

    string EncryptPassword(string plainText);

    string DecryptPassword(string cipherText);
}
