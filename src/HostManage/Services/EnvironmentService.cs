using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using HostManage.Models;
using Newtonsoft.Json;

namespace HostManage.Services;

public class EnvironmentService : IEnvironmentService
{
    private readonly IHostFileService _hostFileService;
    private readonly ILogService _logService;
    private readonly IProxyService _proxyService;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly string _dataDirectory;
    private readonly string _environmentsFilePath;

    public ObservableCollection<HostEnvironment> Environments { get; private set; } = new();

    public HostEnvironment? CurrentEnvironment => Environments.FirstOrDefault(e => e.IsActive);

    public EnvironmentService(IHostFileService hostFileService, ILogService logService, IProxyService proxyService)
    {
        _hostFileService = hostFileService;
        _logService = logService;
        _proxyService = proxyService;

        _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HostManage");
        _environmentsFilePath = Path.Combine(_dataDirectory, "environments.json");
    }

    public async Task CreateEnvironmentAsync(string name, string description, HostEnvironment? copyFrom = null)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("环境名称不能为空", nameof(name));

            if (Environments.Any(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"已存在名为 \"{name}\" 的环境");

            var env = new HostEnvironment
            {
                Name = name,
                Description = description,
                IsActive = false
            };

            if (copyFrom != null)
            {
                env.ProxyId = copyFrom.ProxyId;
                foreach (var rule in copyFrom.Rules)
                {
                    env.Rules.Add(new HostRule
                    {
                        IP = rule.IP,
                        Domain = rule.Domain,
                        Comment = rule.Comment,
                        IsEnabled = rule.IsEnabled,
                        Status = rule.Status
                    });
                }
            }

            Environments.Add(env);
            await SaveEnvironmentsInternalAsync().ConfigureAwait(false);

            _logService.LogAction("创建环境", $"成功创建环境 \"{name}\"", copyFrom != null ? $"从环境 \"{copyFrom.Name}\" 复制规则" : null);
        }
        catch (Exception ex)
        {
            _logService.LogError("创建环境", $"创建环境 \"{name}\" 失败: {ex.Message}", ex.ToString());
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteEnvironmentAsync(Guid envId)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var env = Environments.FirstOrDefault(e => e.Id == envId);
            if (env == null)
                throw new ArgumentException($"未找到ID为 {envId} 的环境", nameof(envId));

            if (env.IsActive)
                throw new InvalidOperationException("不能删除当前活动的环境");

            var name = env.Name;
            Environments.Remove(env);
            await SaveEnvironmentsInternalAsync().ConfigureAwait(false);

            _logService.LogAction("删除环境", $"成功删除环境 \"{name}\"");
        }
        catch (Exception ex)
        {
            _logService.LogError("删除环境", $"删除环境失败: {ex.Message}", ex.ToString());
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SwitchEnvironmentAsync(Guid envId)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var env = Environments.FirstOrDefault(e => e.Id == envId);
            if (env == null)
                throw new ArgumentException($"未找到ID为 {envId} 的环境", nameof(envId));

            foreach (var e in Environments)
            {
                e.IsActive = e.Id == envId;
            }

            var rules = env.Rules.Where(r => r.IsEnabled).ToList();
            await _hostFileService.WriteSystemHostsAsync(rules, true).ConfigureAwait(false);

            ProxyConfig? proxyConfig = null;
            if (env.ProxyId.HasValue)
            {
                proxyConfig = _proxyService.Proxies.FirstOrDefault(p => p.Id == env.ProxyId.Value);
            }

            if (proxyConfig != null)
            {
                await _proxyService.ApplySystemProxyAsync(proxyConfig).ConfigureAwait(false);
            }
            else
            {
                await _proxyService.ClearSystemProxyAsync().ConfigureAwait(false);
            }

            await SaveEnvironmentsInternalAsync().ConfigureAwait(false);

            _logService.LogAction("切换环境", $"成功切换到环境 \"{env.Name}\"", $"规则数: {rules.Count}, 代理: {proxyConfig?.Name ?? "无"}");
        }
        catch (Exception ex)
        {
            _logService.LogError("切换环境", $"切换环境失败: {ex.Message}", ex.ToString());
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateEnvironmentAsync(HostEnvironment env)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var existing = Environments.FirstOrDefault(e => e.Id == env.Id);
            if (existing == null)
                throw new ArgumentException($"未找到ID为 {env.Id} 的环境", nameof(env));

            if (Environments.Any(e => e.Id != env.Id && e.Name.Equals(env.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"已存在名为 \"{env.Name}\" 的环境");

            existing.Name = env.Name;
            existing.Description = env.Description;
            existing.ProxyId = env.ProxyId;
            existing.Rules = new ObservableCollection<HostRule>(env.Rules);
            existing.UpdatedAt = DateTime.Now;

            if (existing.IsActive)
            {
                var rules = existing.Rules.Where(r => r.IsEnabled).ToList();
                await _hostFileService.WriteSystemHostsAsync(rules, true).ConfigureAwait(false);

                ProxyConfig? proxyConfig = null;
                if (existing.ProxyId.HasValue)
                {
                    proxyConfig = _proxyService.Proxies.FirstOrDefault(p => p.Id == existing.ProxyId.Value);
                }

                if (proxyConfig != null)
                {
                    await _proxyService.ApplySystemProxyAsync(proxyConfig).ConfigureAwait(false);
                }
                else
                {
                    await _proxyService.ClearSystemProxyAsync().ConfigureAwait(false);
                }
            }

            await SaveEnvironmentsInternalAsync().ConfigureAwait(false);

            _logService.LogAction("更新环境", $"成功更新环境 \"{existing.Name}\"");
        }
        catch (Exception ex)
        {
            _logService.LogError("更新环境", $"更新环境失败: {ex.Message}", ex.ToString());
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<DiffItem>> CompareWithSystemHostsAsync(Guid envId)
    {
        var diffItems = new List<DiffItem>();

        try
        {
            var env = Environments.FirstOrDefault(e => e.Id == envId);
            if (env == null)
                throw new ArgumentException($"未找到ID为 {envId} 的环境", nameof(envId));

            var systemRules = await _hostFileService.ReadSystemHostsAsync().ConfigureAwait(false);
            var envRules = env.Rules.Where(r => r.IsEnabled).ToList();

            var systemDict = new Dictionary<string, HostRule>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in systemRules)
            {
                var key = rule.Domain.Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(key))
                    systemDict[key] = rule;
            }

            var envDict = new Dictionary<string, HostRule>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in envRules)
            {
                var key = rule.Domain.Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(key))
                    envDict[key] = rule;
            }

            foreach (var kvp in envDict)
            {
                var domain = kvp.Key;
                var envRule = kvp.Value;

                if (!systemDict.TryGetValue(domain, out var sysRule))
                {
                    diffItems.Add(new DiffItem
                    {
                        Type = DiffType.Added,
                        Property = domain,
                        OldValue = null,
                        NewValue = $"{envRule.IP} ({envRule.Comment})"
                    });
                }
                else if (!envRule.IP.Equals(sysRule.IP, StringComparison.OrdinalIgnoreCase))
                {
                    diffItems.Add(new DiffItem
                    {
                        Type = DiffType.Modified,
                        Property = domain,
                        OldValue = sysRule.IP,
                        NewValue = envRule.IP
                    });
                }
            }

            foreach (var kvp in systemDict)
            {
                var domain = kvp.Key;
                if (!envDict.ContainsKey(domain))
                {
                    var sysRule = kvp.Value;
                    diffItems.Add(new DiffItem
                    {
                        Type = DiffType.Removed,
                        Property = domain,
                        OldValue = $"{sysRule.IP} ({sysRule.Comment})",
                        NewValue = null
                    });
                }
            }

            _logService.LogInfo("对比环境", $"对比环境 \"{env.Name}\" 与系统hosts，发现 {diffItems.Count} 处差异");
        }
        catch (Exception ex)
        {
            _logService.LogError("对比环境", $"对比环境与系统hosts失败: {ex.Message}", ex.ToString());
            throw;
        }

        return diffItems;
    }

    public async Task SaveEnvironmentsAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await SaveEnvironmentsInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task LoadEnvironmentsAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
            }

            if (File.Exists(_environmentsFilePath))
            {
                var json = await File.ReadAllTextAsync(_environmentsFilePath).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var list = JsonConvert.DeserializeObject<List<HostEnvironment>>(json);
                    if (list != null && list.Count > 0)
                    {
                        Environments = new ObservableCollection<HostEnvironment>(list);
                        return;
                    }
                }
            }

            CreateDefaultEnvironment();
            await SaveEnvironmentsInternalAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logService.LogError("加载环境", $"加载环境配置失败: {ex.Message}", ex.ToString());
            Environments.Clear();
            CreateDefaultEnvironment();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void CreateDefaultEnvironment()
    {
        var defaultEnv = new HostEnvironment
        {
            Name = "默认环境",
            Description = "系统默认环境",
            IsActive = true
        };
        Environments.Add(defaultEnv);
    }

    private async Task SaveEnvironmentsInternalAsync()
    {
        try
        {
            if (!Directory.Exists(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
            }

            var list = Environments.ToList();
            var json = JsonConvert.SerializeObject(list, Formatting.Indented);
            await File.WriteAllTextAsync(_environmentsFilePath, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logService.LogError("保存环境", $"保存环境配置失败: {ex.Message}", ex.ToString());
            throw;
        }
    }
}
