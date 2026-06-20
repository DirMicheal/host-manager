using System.Collections.ObjectModel;
using HostManage.Models;
using HostManage.Services;

namespace HostManage.ViewModels;

public class LogsViewModel : ViewModelBase
{
    private readonly ILogService _logService;

    private string _searchKeyword = string.Empty;
    private DateTime? _startDate;
    private DateTime? _endDate;
    private LogLevel? _selectedLevel;
    private ObservableCollection<OperationLog> _filteredLogs = new();

    public string SearchKeyword
    {
        get => _searchKeyword;
        set => SetProperty(ref _searchKeyword, value);
    }

    public DateTime? StartDate
    {
        get => _startDate;
        set => SetProperty(ref _startDate, value);
    }

    public DateTime? EndDate
    {
        get => _endDate;
        set => SetProperty(ref _endDate, value);
    }

    public LogLevel? SelectedLevel
    {
        get => _selectedLevel;
        set => SetProperty(ref _selectedLevel, value);
    }

    public ObservableCollection<OperationLog> FilteredLogs
    {
        get => _filteredLogs;
        set => SetProperty(ref _filteredLogs, value);
    }

    public List<LogLevel?> LevelOptions { get; } = new()
    {
        null,
        LogLevel.Info,
        LogLevel.Warn,
        LogLevel.Error,
        LogLevel.Action
    };

    public AsyncRelayCommand SearchCommand { get; }
    public RelayCommand ClearCommand { get; }
    public AsyncRelayCommand ExportCommand { get; }

    public LogsViewModel(ILogService logService)
    {
        _logService = logService;

        SearchCommand = new AsyncRelayCommand(ExecuteSearchAsync);
        ClearCommand = new RelayCommand(ExecuteClear);
        ExportCommand = new AsyncRelayCommand(ExecuteExportAsync, () => FilteredLogs.Count > 0);
    }

    public override async Task LoadAsync()
    {
        _logService.LogAction("Logs_Load", "开始加载日志列表");
        IsBusy = true;
        BusyMessage = "正在加载日志...";

        try
        {
            await ExecuteSearchAsync();
            _logService.LogAction("Logs_Load", $"日志加载完成，共 {FilteredLogs.Count} 条");
        }
        catch (Exception ex)
        {
            _logService.LogError("Logs_Load", "日志加载失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ExecuteSearchAsync()
    {
        _logService.LogAction("Logs_Search",
            $"搜索日志 - 关键字: {SearchKeyword}, " +
            $"开始日期: {StartDate?.ToShortDateString() ?? "无"}, " +
            $"结束日期: {EndDate?.ToShortDateString() ?? "无"}, " +
            $"级别: {SelectedLevel?.ToString() ?? "全部"}");

        IsBusy = true;
        BusyMessage = "正在搜索日志...";

        try
        {
            var end = EndDate.HasValue ? EndDate.Value.Date.AddDays(1).AddTicks(-1) : (DateTime?)null;
            var logs = await _logService.SearchLogsAsync(SearchKeyword, StartDate, end);

            if (SelectedLevel.HasValue)
            {
                logs = logs.Where(l => l.Level == SelectedLevel.Value).ToList();
            }

            FilteredLogs = new ObservableCollection<OperationLog>(logs.OrderByDescending(l => l.Timestamp));
            ExportCommand.RaiseCanExecuteChanged();
            _logService.LogAction("Logs_Search", $"搜索完成，找到 {FilteredLogs.Count} 条日志");
        }
        catch (Exception ex)
        {
            _logService.LogError("Logs_Search", "日志搜索失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private void ExecuteClear()
    {
        _logService.LogAction("Logs_Clear", "清空搜索条件");

        SearchKeyword = string.Empty;
        StartDate = null;
        EndDate = null;
        SelectedLevel = null;
        FilteredLogs = new ObservableCollection<OperationLog>(_logService.Logs.OrderByDescending(l => l.Timestamp));
        ExportCommand.RaiseCanExecuteChanged();
    }

    private async Task ExecuteExportAsync()
    {
        _logService.LogAction("Logs_Export", $"准备导出 {FilteredLogs.Count} 条日志");
        IsBusy = true;
        BusyMessage = "正在导出日志...";

        try
        {
            var filePath = $"logs_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            await _logService.ExportLogsAsync(filePath, FilteredLogs);
            _logService.LogAction("Logs_Export", $"日志已导出到: {filePath}");
        }
        catch (Exception ex)
        {
            _logService.LogError("Logs_Export", "日志导出失败", ex.ToString());
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }
}
