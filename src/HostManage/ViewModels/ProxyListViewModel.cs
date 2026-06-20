using System.Collections.ObjectModel;
using HostManage.Models;
using HostManage.Services;

namespace HostManage.ViewModels;

public class ProxyListViewModel : ViewModelBase
{
    private readonly IProxyService _proxyService;
    private readonly ILogService _logService;
    private readonly INetworkService _networkService;

    private ProxyConfig? _selectedProxy;
    private ProxyConfig? _currentSystemProxy;

    public ObservableCollection<ProxyConfig> Proxies => _proxyService.Proxies;

    public ProxyConfig? SelectedProxy
    {
        get => _selectedProxy;
        set
        {
            SetProperty(ref _selectedProxy, value);
            RaiseCommands();
        }
    }

    public ProxyConfig? CurrentSystemProxy
    {
        get => _currentSystemProxy;
        set => SetProperty(ref _currentSystemProxy, value);
    }

    public AsyncRelayCommand AddCommand { get; }
    public AsyncRelayCommand EditCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand TestCommand { get; }
    public AsyncRelayCommand ApplyCommand { get; }

    public ProxyListViewModel(
        IProxyService proxyService,
        ILogService logService,
        INetworkService networkService)
    {
        _proxyService = proxyService;
        _logService = logService;
        _networkService = networkService;

        AddCommand = new AsyncRelayCommand(ExecuteAddAsync);
        EditCommand = new AsyncRelayCommand(ExecuteEditAsync, () => SelectedProxy != null);
        DeleteCommand = new AsyncRelayCommand(ExecuteDeleteAsync, () => SelectedProxy != null);
        TestCommand = new AsyncRelayCommand(ExecuteTestAsync, () => SelectedProxy != null);
        ApplyCommand = new AsyncRelayCommand(ExecuteApplyAsync, () => SelectedProxy != null);
    }

    public override async Task LoadAsync()
    {
        _logService.LogAction("ProxyList_Load", "开始加载代理列表");
        IsBusy = true;
        BusyMessage = "正在加载代理配置...";

        try
        {
            CurrentSystemProxy = await _proxyService.GetCurrentSystemProxyAsync();
            _logService.LogAction("ProxyList_Load", $"代理列表加载完成，共 {Proxies.Count} 个配置");
        }
        catch (Exception ex)
        {
            _logService.LogError("ProxyList_Load", "代理列表加载失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private void RaiseCommands()
    {
        EditCommand.RaiseCanExecuteChanged();
        DeleteCommand.RaiseCanExecuteChanged();
        TestCommand.RaiseCanExecuteChanged();
        ApplyCommand.RaiseCanExecuteChanged();
    }

    private Task ExecuteAddAsync()
    {
        _logService.LogAction("Proxy_Add", "准备添加代理配置");
        return Task.CompletedTask;
    }

    private async Task ExecuteEditAsync()
    {
        if (SelectedProxy == null) return;

        _logService.LogAction("Proxy_Edit", $"准备编辑代理: {SelectedProxy.Name}");
        IsBusy = true;
        BusyMessage = "正在保存代理配置...";

        try
        {
            if (!string.IsNullOrEmpty(SelectedProxy.Password))
            {
                SelectedProxy.EncryptedPassword = _proxyService.EncryptPassword(SelectedProxy.Password);
            }

            await _proxyService.UpdateProxyAsync(SelectedProxy);
            _logService.LogAction("Proxy_Edit", "代理配置保存成功");
        }
        catch (Exception ex)
        {
            _logService.LogError("Proxy_Edit", "代理配置保存失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteDeleteAsync()
    {
        if (SelectedProxy == null) return;

        _logService.LogAction("Proxy_Delete", $"准备删除代理: {SelectedProxy.Name}");
        IsBusy = true;
        BusyMessage = "正在删除代理配置...";

        try
        {
            await _proxyService.DeleteProxyAsync(SelectedProxy.Id);
            SelectedProxy = null;
            _logService.LogAction("Proxy_Delete", "代理配置删除成功");
        }
        catch (Exception ex)
        {
            _logService.LogError("Proxy_Delete", "代理配置删除失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteTestAsync()
    {
        if (SelectedProxy == null) return;

        _logService.LogAction("Proxy_Test", $"开始测试代理连接: {SelectedProxy.Name}");
        IsBusy = true;
        BusyMessage = "正在测试代理连接...";

        try
        {
            var pingResult = await _networkService.PingAsync(SelectedProxy.Host, 5000);
            var portResult = await _networkService.TestPortAsync(SelectedProxy.Host, SelectedProxy.Port, 5000);

            _logService.LogAction("Proxy_Test",
                $"测试完成 - Ping: {(pingResult.Success ? $"{pingResult.RoundtripTime}ms" : "失败")}, " +
                $"端口: {(portResult.IsOpen ? "开放" : "关闭")}");
        }
        catch (Exception ex)
        {
            _logService.LogError("Proxy_Test", "代理连接测试失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteApplyAsync()
    {
        if (SelectedProxy == null) return;

        _logService.LogAction("Proxy_Apply", $"准备应用代理: {SelectedProxy.Name}");
        IsBusy = true;
        BusyMessage = "正在应用系统代理...";

        try
        {
            await _proxyService.ApplySystemProxyAsync(SelectedProxy);
            CurrentSystemProxy = SelectedProxy;
            _logService.LogAction("Proxy_Apply", "系统代理应用成功");
        }
        catch (Exception ex)
        {
            _logService.LogError("Proxy_Apply", "系统代理应用失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }
}
