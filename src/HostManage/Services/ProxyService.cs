using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HostManage.Models;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace HostManage.Services;

public class ProxyService : IProxyService
{
    private static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HostManage");
    private static readonly string ProxiesFilePath = Path.Combine(AppDataPath, "proxies.json");
    private static readonly string EncryptionKeyFilePath = Path.Combine(AppDataPath, "encryption.key");

    private readonly byte[] _encryptionKey;

    public ObservableCollection<ProxyConfig> Proxies { get; private set; }

    public ProxyService()
    {
        Proxies = new ObservableCollection<ProxyConfig>();
        EnsureAppDataDirectory();
        _encryptionKey = GetOrCreateEncryptionKey();
        _ = LoadProxiesAsync();
    }

    private static void EnsureAppDataDirectory()
    {
        if (!Directory.Exists(AppDataPath))
        {
            Directory.CreateDirectory(AppDataPath);
        }
    }

    private static byte[] GetOrCreateEncryptionKey()
    {
        if (File.Exists(EncryptionKeyFilePath))
        {
            return File.ReadAllBytes(EncryptionKeyFilePath);
        }

        var key = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(key);
        }
        File.WriteAllBytes(EncryptionKeyFilePath, key);
        return key;
    }

    private async Task LoadProxiesAsync()
    {
        try
        {
            if (File.Exists(ProxiesFilePath))
            {
                var json = await File.ReadAllTextAsync(ProxiesFilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var list = JsonConvert.DeserializeObject<List<ProxyConfig>>(json);
                    if (list != null)
                    {
                        foreach (var proxy in list)
                        {
                            Proxies.Add(proxy);
                        }
                    }
                }
            }
        }
        catch
        {
            Proxies = new ObservableCollection<ProxyConfig>();
        }
    }

    private async Task SaveProxiesAsync()
    {
        try
        {
            EnsureAppDataDirectory();
            var json = JsonConvert.SerializeObject(Proxies.ToList(), Formatting.Indented);
            await File.WriteAllTextAsync(ProxiesFilePath, json);
        }
        catch
        {
        }
    }

    public async Task AddProxyAsync(ProxyConfig proxy)
    {
        if (proxy == null)
            throw new ArgumentNullException(nameof(proxy));

        if (proxy.Id == Guid.Empty)
            proxy.Id = Guid.NewGuid();

        if (!string.IsNullOrWhiteSpace(proxy.Password))
        {
            proxy.EncryptedPassword = EncryptPassword(proxy.Password);
            proxy.Password = string.Empty;
        }

        Proxies.Add(proxy);
        await SaveProxiesAsync();
    }

    public async Task DeleteProxyAsync(Guid proxyId)
    {
        var proxy = Proxies.FirstOrDefault(p => p.Id == proxyId);
        if (proxy != null)
        {
            Proxies.Remove(proxy);
            await SaveProxiesAsync();
        }
    }

    public async Task UpdateProxyAsync(ProxyConfig proxy)
    {
        if (proxy == null)
            throw new ArgumentNullException(nameof(proxy));

        var existing = Proxies.FirstOrDefault(p => p.Id == proxy.Id);
        if (existing == null)
            return;

        var index = Proxies.IndexOf(existing);

        if (!string.IsNullOrWhiteSpace(proxy.Password))
        {
            proxy.EncryptedPassword = EncryptPassword(proxy.Password);
            proxy.Password = string.Empty;
        }

        Proxies[index] = proxy;
        await SaveProxiesAsync();
    }

    public string EncryptPassword(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encryptedBytes);
            }
            else
            {
                return AesEncrypt(plainText);
            }
        }
        catch
        {
            return AesEncrypt(plainText);
        }
    }

    public string DecryptPassword(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var encryptedBytes = Convert.FromBase64String(cipherText);
                var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            else
            {
                return AesDecrypt(cipherText);
            }
        }
        catch
        {
            try
            {
                return AesDecrypt(cipherText);
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    private string AesEncrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.GenerateIV();

        var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }

        var iv = aes.IV;
        var encrypted = ms.ToArray();
        var result = new byte[iv.Length + encrypted.Length];
        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
        Buffer.BlockCopy(encrypted, 0, result, iv.Length, encrypted.Length);
        return Convert.ToBase64String(result);
    }

    private string AesDecrypt(string cipherText)
    {
        var fullCipher = Convert.FromBase64String(cipherText);
        var iv = new byte[16];
        var cipher = new byte[fullCipher.Length - iv.Length];
        Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.IV = iv;

        var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(cipher);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        return sr.ReadToEnd();
    }

    public async Task ApplySystemProxyAsync(ProxyConfig? proxy)
    {
        if (proxy == null)
        {
            await ClearSystemProxyAsync();
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);
            if (key == null)
                return;

            key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);

            var proxyServer = BuildProxyServerString(proxy);
            key.SetValue("ProxyServer", proxyServer, RegistryValueKind.String);

            if (!string.IsNullOrWhiteSpace(proxy.Username))
            {
                key.SetValue("ProxyUser", proxy.Username, RegistryValueKind.String);
                if (!string.IsNullOrWhiteSpace(proxy.EncryptedPassword))
                {
                    var decryptedPassword = DecryptPassword(proxy.EncryptedPassword);
                    key.SetValue("ProxyPass", decryptedPassword, RegistryValueKind.String);
                }
            }

            RefreshSystemProxy();
        }
        catch
        {
        }

        await Task.CompletedTask;
    }

    public async Task ClearSystemProxyAsync()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);
            if (key == null)
                return;

            key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
            key.DeleteValue("ProxyServer", false);
            key.DeleteValue("ProxyUser", false);
            key.DeleteValue("ProxyPass", false);

            RefreshSystemProxy();
        }
        catch
        {
        }

        await Task.CompletedTask;
    }

    public async Task<ProxyConfig?> GetCurrentSystemProxyAsync()
    {
        try
        {
            await Task.CompletedTask;
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", false);
            if (key == null)
                return null;

            var enableValue = key.GetValue("ProxyEnable");
            if (enableValue == null || (int)enableValue == 0)
                return null;

            var proxyServer = key.GetValue("ProxyServer") as string;
            if (string.IsNullOrWhiteSpace(proxyServer))
                return null;

            var proxy = ParseProxyServerString(proxyServer);
            if (proxy != null)
            {
                proxy.IsSystemProxy = true;

                var username = key.GetValue("ProxyUser") as string;
                if (!string.IsNullOrWhiteSpace(username))
                {
                    proxy.Username = username;
                }
            }

            return proxy;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildProxyServerString(ProxyConfig proxy)
    {
        var typePrefix = proxy.Type switch
        {
            ProxyType.Http => "http",
            ProxyType.Https => "https",
            ProxyType.Socks5 => "socks",
            _ => "http"
        };
        return $"{typePrefix}={proxy.Host}:{proxy.Port}";
    }

    private static ProxyConfig? ParseProxyServerString(string proxyServer)
    {
        try
        {
            var proxy = new ProxyConfig();
            var parts = proxyServer.Split(';');

            if (parts.Length == 1)
            {
                var singlePart = parts[0];
                if (singlePart.Contains('='))
                {
                    var typeAndAddr = singlePart.Split('=', 2);
                    if (typeAndAddr.Length == 2)
                    {
                        proxy.Type = ParseProxyType(typeAndAddr[0]);
                        ParseHostAndPort(typeAndAddr[1], proxy);
                    }
                }
                else
                {
                    proxy.Type = ProxyType.Http;
                    ParseHostAndPort(singlePart, proxy);
                }
            }
            else
            {
                foreach (var part in parts)
                {
                    if (part.Contains('='))
                    {
                        var typeAndAddr = part.Split('=', 2);
                        if (typeAndAddr.Length == 2)
                        {
                            var type = ParseProxyType(typeAndAddr[0]);
                            if (proxy.Type == ProxyType.None)
                            {
                                proxy.Type = type;
                                ParseHostAndPort(typeAndAddr[1], proxy);
                            }
                        }
                    }
                }
            }

            return proxy.Host != null && proxy.Host.Length > 0 ? proxy : null;
        }
        catch
        {
            return null;
        }
    }

    private static ProxyType ParseProxyType(string typeStr)
    {
        return typeStr.Trim().ToLower() switch
        {
            "http" => ProxyType.Http,
            "https" => ProxyType.Https,
            "socks" => ProxyType.Socks5,
            "socks5" => ProxyType.Socks5,
            _ => ProxyType.Http
        };
    }

    private static void ParseHostAndPort(string hostAndPort, ProxyConfig proxy)
    {
        var hp = hostAndPort.Split(':');
        if (hp.Length >= 1)
        {
            proxy.Host = hp[0].Trim();
        }
        if (hp.Length >= 2 && int.TryParse(hp[1], out var port))
        {
            proxy.Port = port;
        }
    }

    private static void RefreshSystemProxy()
    {
        try
        {
            NativeMethods.InternetSetOption(IntPtr.Zero, NativeMethods.INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            NativeMethods.InternetSetOption(IntPtr.Zero, NativeMethods.INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }
        catch
        {
        }
    }

    private static class NativeMethods
    {
        public const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        public const int INTERNET_OPTION_REFRESH = 37;

        [System.Runtime.InteropServices.DllImport("wininet.dll", SetLastError = true)]
        public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
    }
}
