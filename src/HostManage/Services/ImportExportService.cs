using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HostManage.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace HostManage.Services;

public class ImportExportService : IImportExportService
{
    private readonly ILogger<ImportExportService> _logger;
    private readonly IEnvironmentService _environmentService;
    private readonly IProxyService _proxyService;
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;

    public ImportExportService(
        ILogger<ImportExportService> logger,
        IEnvironmentService environmentService,
        IProxyService proxyService,
        ISettingsService settingsService,
        ILogService logService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
        _proxyService = proxyService ?? throw new ArgumentNullException(nameof(proxyService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    public async Task<List<HostRule>> ImportFromHostsFileAsync(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var rules = new List<HostRule>();

        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"文件不存在: {filePath}", filePath);
            }

            var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            rules = ParseHostsContent(content);

            _logService.LogAction("导入Hosts文件", $"成功从 hosts 文件导入 {rules.Count} 条规则", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入 hosts 文件失败: {FilePath}", filePath);
            _logService.LogError("导入Hosts文件", $"导入失败: {ex.Message}", ex.ToString());
            throw;
        }

        return rules;
    }

    public async Task<List<HostRule>> ImportFromJsonAsync(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var rules = new List<HostRule>();

        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"文件不存在: {filePath}", filePath);
            }

            var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            var list = JsonConvert.DeserializeObject<List<HostRule>>(json);

            if (list != null)
            {
                rules = list.Where(r => !string.IsNullOrWhiteSpace(r.IP) && !string.IsNullOrWhiteSpace(r.Domain)).ToList();

                foreach (var rule in rules)
                {
                    if (rule.Id == Guid.Empty)
                    {
                        rule.Id = Guid.NewGuid();
                    }
                    if (rule.CreatedAt == default)
                    {
                        rule.CreatedAt = DateTime.Now;
                    }
                    if (rule.UpdatedAt == default)
                    {
                        rule.UpdatedAt = DateTime.Now;
                    }
                }
            }

            _logService.LogAction("导入JSON", $"成功从 JSON 文件导入 {rules.Count} 条规则", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入 JSON 文件失败: {FilePath}", filePath);
            _logService.LogError("导入JSON", $"导入失败: {ex.Message}", ex.ToString());
            throw;
        }

        return rules;
    }

    public async Task<List<HostRule>> ImportFromCsvAsync(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var rules = new List<HostRule>();

        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"文件不存在: {filePath}", filePath);
            }

            var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);

            if (lines.Length == 0)
            {
                return rules;
            }

            var startIndex = 0;
            var headerLine = lines[0].Trim();
            if (headerLine.Contains("IP", StringComparison.OrdinalIgnoreCase) &&
                headerLine.Contains("Domain", StringComparison.OrdinalIgnoreCase))
            {
                startIndex = 1;
            }

            for (var i = startIndex; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var fields = ParseCsvLine(line);
                if (fields.Count < 2)
                {
                    continue;
                }

                var ip = fields[0].Trim();
                var domain = fields[1].Trim();

                if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(domain))
                {
                    continue;
                }

                var rule = new HostRule
                {
                    IP = ip,
                    Domain = domain,
                    IsEnabled = true,
                    Status = RuleStatus.Active
                };

                if (fields.Count > 2 && bool.TryParse(fields[2].Trim(), out var isEnabled))
                {
                    rule.IsEnabled = isEnabled;
                    rule.Status = isEnabled ? RuleStatus.Active : RuleStatus.Inactive;
                }

                if (fields.Count > 3)
                {
                    rule.Comment = fields[3].Trim();
                }

                rules.Add(rule);
            }

            _logService.LogAction("导入CSV", $"成功从 CSV 文件导入 {rules.Count} 条规则", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入 CSV 文件失败: {FilePath}", filePath);
            _logService.LogError("导入CSV", $"导入失败: {ex.Message}", ex.ToString());
            throw;
        }

        return rules;
    }

    public async Task ExportToHostsFileAsync(string filePath, IEnumerable<HostRule> rules)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(rules);

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var sb = new StringBuilder();
            sb.AppendLine("# =============================================================");
            sb.AppendLine("# Hosts file exported by HostManage");
            sb.AppendLine($"# Exported at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("# =============================================================");
            sb.AppendLine();
            sb.AppendLine("127.0.0.1\tlocalhost");
            sb.AppendLine("::1\t\tlocalhost");
            sb.AppendLine();

            var ruleList = rules.ToList();
            var groupedRules = ruleList
                .GroupBy(r => new { r.IP, r.IsEnabled, r.Comment })
                .ToList();

            foreach (var group in groupedRules)
            {
                var domains = group.Select(g => g.Domain).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (domains.Count == 0)
                {
                    continue;
                }

                var firstRule = group.First();
                var line = BuildHostLine(firstRule, domains);
                sb.AppendLine(line);
            }

            await File.WriteAllTextAsync(filePath, sb.ToString(), new UTF8Encoding(true));

            _logService.LogAction("导出Hosts文件", $"成功导出 {ruleList.Count} 条规则到 hosts 格式文件", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出 hosts 文件失败: {FilePath}", filePath);
            _logService.LogError("导出Hosts文件", $"导出失败: {ex.Message}", ex.ToString());
            throw;
        }
    }

    public async Task ExportToJsonAsync(string filePath, IEnumerable<HostRule> rules)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(rules);

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var ruleList = rules.ToList();
            var json = JsonConvert.SerializeObject(ruleList, Formatting.Indented);
            await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);

            _logService.LogAction("导出JSON", $"成功导出 {ruleList.Count} 条规则到 JSON 文件", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出 JSON 文件失败: {FilePath}", filePath);
            _logService.LogError("导出JSON", $"导出失败: {ex.Message}", ex.ToString());
            throw;
        }
    }

    public async Task ExportToCsvAsync(string filePath, IEnumerable<HostRule> rules)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(rules);

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var sb = new StringBuilder();
            sb.AppendLine("IP,Domain,IsEnabled,Comment");

            var ruleList = rules.ToList();
            foreach (var rule in ruleList)
            {
                sb.Append(CsvEscape(rule.IP));
                sb.Append(',');
                sb.Append(CsvEscape(rule.Domain));
                sb.Append(',');
                sb.Append(rule.IsEnabled.ToString().ToLowerInvariant());
                sb.Append(',');
                sb.AppendLine(CsvEscape(rule.Comment));
            }

            await File.WriteAllTextAsync(filePath, sb.ToString(), new UTF8Encoding(true));

            _logService.LogAction("导出CSV", $"成功导出 {ruleList.Count} 条规则到 CSV 文件", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出 CSV 文件失败: {FilePath}", filePath);
            _logService.LogError("导出CSV", $"导出失败: {ex.Message}", ex.ToString());
            throw;
        }
    }

    public Task<string> ExportFullConfigAsync()
    {
        try
        {
            var config = new FullConfig
            {
                Version = 1,
                ExportedAt = DateTime.Now,
                Environments = _environmentService.Environments.ToList(),
                Proxies = _proxyService.Proxies.ToList(),
                Settings = _settingsService.Current
            };

            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            var compressed = CompressString(json);

            _logService.LogAction("导出完整配置", "成功导出完整配置");

            return Task.FromResult(compressed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出完整配置失败");
            _logService.LogError("导出完整配置", $"导出失败: {ex.Message}", ex.ToString());
            return Task.FromException<string>(ex);
        }
    }

    public async Task ImportFullConfigAsync(string jsonConfig)
    {
        ArgumentNullException.ThrowIfNull(jsonConfig);

        try
        {
            var json = DecompressString(jsonConfig);
            var config = JsonConvert.DeserializeObject<FullConfig>(json);

            if (config == null)
            {
                throw new InvalidDataException("无效的配置数据");
            }

            if (config.Environments != null && config.Environments.Count > 0)
            {
                foreach (var env in config.Environments)
                {
                    await _environmentService.CreateEnvironmentAsync(env.Name, env.Description);

                    var newEnv = _environmentService.Environments.FirstOrDefault(e => e.Name == env.Name);
                    if (newEnv != null)
                    {
                        newEnv.ProxyId = env.ProxyId;

                        foreach (var rule in env.Rules)
                        {
                            rule.Id = Guid.NewGuid();
                            rule.CreatedAt = DateTime.Now;
                            rule.UpdatedAt = DateTime.Now;
                            newEnv.Rules.Add(rule);
                        }
                    }
                }
            }

            if (config.Proxies != null && config.Proxies.Count > 0)
            {
                foreach (var proxy in config.Proxies)
                {
                    proxy.Id = Guid.NewGuid();
                    if (!string.IsNullOrEmpty(proxy.Password))
                    {
                        proxy.EncryptedPassword = _proxyService.EncryptPassword(proxy.Password);
                    }
                    await _proxyService.AddProxyAsync(proxy);
                }
            }

            if (config.Settings != null)
            {
                if (config.Settings.Theme != default)
                {
                    _settingsService.UpdateTheme(config.Settings.Theme);
                }
            }

            await _environmentService.SaveEnvironmentsAsync();
            await _settingsService.SaveSettingsAsync();

            _logService.LogAction("导入完整配置", "成功导入完整配置");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入完整配置失败");
            _logService.LogError("导入完整配置", $"导入失败: {ex.Message}", ex.ToString());
            throw;
        }
    }

    private static List<HostRule> ParseHostsContent(string content)
    {
        var rules = new List<HostRule>();

        if (string.IsNullOrWhiteSpace(content))
        {
            return rules;
        }

        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        for (var i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var isDisabled = false;
            var workingLine = line;

            if (workingLine.StartsWith('#'))
            {
                var afterHash = workingLine[1..].TrimStart();
                if (string.IsNullOrWhiteSpace(afterHash))
                {
                    continue;
                }

                var firstToken = Regex.Split(afterHash, @"[\s\t]+")[0];
                if (!IPAddress.TryParse(firstToken, out _))
                {
                    continue;
                }

                isDisabled = true;
                workingLine = afterHash;
            }

            var comment = string.Empty;
            var commentIndex = FindCommentIndex(workingLine);
            if (commentIndex >= 0)
            {
                comment = workingLine[(commentIndex + 1)..].Trim();
                workingLine = workingLine[..commentIndex].Trim();
            }

            if (string.IsNullOrWhiteSpace(workingLine))
            {
                continue;
            }

            var parts = Regex.Split(workingLine, @"[\s\t]+")
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            if (parts.Count < 2)
            {
                continue;
            }

            var ip = parts[0];
            if (!IPAddress.TryParse(ip, out var address))
            {
                continue;
            }

            if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork &&
                address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                continue;
            }

            var domains = parts.Skip(1).ToList();

            for (var j = 0; j < domains.Count; j++)
            {
                var rule = new HostRule
                {
                    IP = ip,
                    Domain = domains[j],
                    Comment = j == 0 ? comment : string.Empty,
                    IsEnabled = !isDisabled,
                    Status = !isDisabled ? RuleStatus.Active : RuleStatus.Inactive
                };

                rules.Add(rule);
            }
        }

        return rules;
    }

    private static string BuildHostLine(HostRule rule, List<string> domains)
    {
        var sb = new StringBuilder();

        if (!rule.IsEnabled)
        {
            sb.Append("# ");
        }

        sb.Append(rule.IP);

        var tabCount = rule.IP.Length < 16 ? 2 : 1;
        sb.Append(new string('\t', tabCount));

        sb.Append(string.Join(' ', domains));

        if (!string.IsNullOrWhiteSpace(rule.Comment))
        {
            sb.Append(" # ");
            sb.Append(rule.Comment.Trim());
        }

        return sb.ToString().TrimEnd();
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static int FindCommentIndex(string line)
    {
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (line[i] == '#' && !inQuotes)
            {
                var hasSpaceBefore = i == 0 || char.IsWhiteSpace(line[i - 1]);
                if (hasSpaceBefore || i == 0)
                {
                    return i;
                }
            }
        }
        return -1;
    }

    private static string CompressString(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }
        return Convert.ToBase64String(output.ToArray());
    }

    private static string DecompressString(string compressedText)
    {
        var bytes = Convert.FromBase64String(compressedText);
        using var input = new MemoryStream(bytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private class FullConfig
    {
        public int Version { get; set; }

        public DateTime ExportedAt { get; set; }

        public List<HostEnvironment> Environments { get; set; } = new();

        public List<ProxyConfig> Proxies { get; set; } = new();

        public AppSettings? Settings { get; set; }
    }
}
