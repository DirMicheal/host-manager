using HostManage.Models;

namespace HostManage.Services;

public interface IImportExportService
{
    Task<List<HostRule>> ImportFromHostsFileAsync(string filePath);

    Task<List<HostRule>> ImportFromJsonAsync(string filePath);

    Task<List<HostRule>> ImportFromCsvAsync(string filePath);

    Task ExportToHostsFileAsync(string filePath, IEnumerable<HostRule> rules);

    Task ExportToJsonAsync(string filePath, IEnumerable<HostRule> rules);

    Task ExportToCsvAsync(string filePath, IEnumerable<HostRule> rules);

    Task<string> ExportFullConfigAsync();

    Task ImportFullConfigAsync(string jsonConfig);
}
