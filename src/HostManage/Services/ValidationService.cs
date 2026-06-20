using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using HostManage.Models;
using Microsoft.Extensions.Logging;

namespace HostManage.Services;

public partial class ValidationService : IValidationService
{
    private readonly ILogger<ValidationService> _logger;

    public ValidationService(ILogger<ValidationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ValidationResult ValidateIP(string ip)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ip))
        {
            errors.Add("IP地址不能为空");
            return ValidationResult.Fail(errors.ToArray());
        }

        var trimmedIp = ip.Trim();

        if (!IPAddress.TryParse(trimmedIp, out var address))
        {
            errors.Add("IP地址格式不正确");
            return ValidationResult.Fail(errors.ToArray());
        }

        if (address.AddressFamily != AddressFamily.InterNetwork &&
            address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            errors.Add("仅支持IPv4和IPv6地址");
            return ValidationResult.Fail(errors.ToArray());
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();

            if (bytes.All(b => b == 0))
            {
                errors.Add("0.0.0.0 不是有效的主机IP地址");
                return ValidationResult.Fail(errors.ToArray());
            }

            if (bytes.All(b => b == 255))
            {
                errors.Add("255.255.255.255 是广播地址，不能作为主机IP");
                return ValidationResult.Fail(errors.ToArray());
            }

            if (bytes[0] == 127)
            {
                errors.Add("127.x.x.x 为回环地址，请使用 127.0.0.1 或其他有效IP");
                return ValidationResult.Fail(errors.ToArray());
            }

            if (bytes[0] >= 224 && bytes[0] <= 239)
            {
                errors.Add("D类多播地址（224.0.0.0-239.255.255.255）不能作为主机IP");
                return ValidationResult.Fail(errors.ToArray());
            }

            if (bytes[0] >= 240)
            {
                errors.Add("E类保留地址（240.0.0.0以上）不能作为主机IP");
                return ValidationResult.Fail(errors.ToArray());
            }
        }
        else if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();

            if (bytes.All(b => b == 0))
            {
                errors.Add(":: 不是有效的主机IPv6地址");
                return ValidationResult.Fail(errors.ToArray());
            }
        }

        if (errors.Count > 0)
        {
            return ValidationResult.Fail(errors.ToArray());
        }

        return ValidationResult.Success();
    }

    public ValidationResult ValidateDomain(string domain)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(domain))
        {
            errors.Add("域名不能为空");
            return ValidationResult.Fail(errors.ToArray());
        }

        var trimmedDomain = domain.Trim();

        if (trimmedDomain.Length > 253)
        {
            errors.Add("域名长度不能超过253个字符");
            return ValidationResult.Fail(errors.ToArray());
        }

        var workingDomain = trimmedDomain;
        var hasWildcard = false;

        if (workingDomain.StartsWith("*.", StringComparison.Ordinal))
        {
            hasWildcard = true;
            workingDomain = workingDomain[2..];
        }
        else if (workingDomain.StartsWith("*", StringComparison.Ordinal))
        {
            errors.Add("通配符必须以 *. 开头，例如 *.example.com");
            return ValidationResult.Fail(errors.ToArray());
        }

        if (string.IsNullOrWhiteSpace(workingDomain))
        {
            errors.Add("域名不能为空");
            return ValidationResult.Fail(errors.ToArray());
        }

        var labels = workingDomain.Split('.');

        if (labels.Length < 2 && !hasWildcard)
        {
            errors.Add("域名至少需要包含二级域名，例如 example.com");
            return ValidationResult.Fail(errors.ToArray());
        }

        foreach (var label in labels)
        {
            if (string.IsNullOrEmpty(label))
            {
                errors.Add("域名中存在空的标签段（连续的点）");
                continue;
            }

            if (label.Length > 63)
            {
                errors.Add($"域名标签 \"{label}\" 长度超过63个字符");
                continue;
            }

            if (!DomainLabelRegex().IsMatch(label))
            {
                errors.Add($"域名标签 \"{label}\" 格式不正确，只能包含字母、数字和连字符，且不能以连字符开头或结尾");
            }
        }

        if (errors.Count > 0)
        {
            return ValidationResult.Fail(errors.ToArray());
        }

        return ValidationResult.Success();
    }

    public ValidationResult ValidateHostRule(HostRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var errors = new List<string>();

        var ipResult = ValidateIP(rule.IP);
        if (!ipResult.IsValid)
        {
            errors.AddRange(ipResult.Errors.Select(e => $"IP: {e}"));
        }

        var domainResult = ValidateDomain(rule.Domain);
        if (!domainResult.IsValid)
        {
            errors.AddRange(domainResult.Errors.Select(e => $"域名: {e}"));
        }

        if (errors.Count > 0)
        {
            return ValidationResult.Fail(errors.ToArray());
        }

        return ValidationResult.Success();
    }

    public List<ConflictInfo> ScanConflicts(IEnumerable<HostRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var conflicts = new List<ConflictInfo>();

        try
        {
            var enabledRules = rules
                .Where(r => r.IsEnabled)
                .ToList();

            var domainGroups = enabledRules
                .GroupBy(r => r.Domain, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in domainGroups)
            {
                var distinctIps = group
                    .Select(r => r.IP.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (distinctIps.Count > 1)
                {
                    conflicts.Add(new ConflictInfo
                    {
                        Domain = group.Key,
                        ConflictingRuleIds = group.Select(r => r.Id).ToList()
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "扫描规则冲突时发生错误");
        }

        return conflicts;
    }

    public List<SyntaxError> ParseHostsContent(string content)
    {
        var errors = new List<SyntaxError>();

        if (string.IsNullOrWhiteSpace(content))
        {
            return errors;
        }

        try
        {
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            for (var i = 0; i < lines.Length; i++)
            {
                var lineNumber = i + 1;
                var rawLine = lines[i];
                var line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith('#'))
                {
                    var afterHash = line[1..].TrimStart();
                    if (string.IsNullOrWhiteSpace(afterHash))
                    {
                        continue;
                    }

                    var firstToken = Regex.Split(afterHash, @"[\s\t]+")[0];
                    if (!IPAddress.TryParse(firstToken, out _))
                    {
                        continue;
                    }

                    line = afterHash;
                }

                string workingLine = line;
                var commentIndex = FindCommentIndex(workingLine);
                if (commentIndex >= 0)
                {
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
                    if (IPAddress.TryParse(parts.FirstOrDefault() ?? string.Empty, out _))
                    {
                        errors.Add(new SyntaxError
                        {
                            LineNumber = lineNumber,
                            LineContent = rawLine,
                            Message = "缺少域名部分，IP地址后需要至少一个域名"
                        });
                    }
                    else
                    {
                        errors.Add(new SyntaxError
                        {
                            LineNumber = lineNumber,
                            LineContent = rawLine,
                            Message = "格式不正确，hosts条目格式应为: IP 域名 [域名2...]"
                        });
                    }
                    continue;
                }

                var ip = parts[0];
                if (!IPAddress.TryParse(ip, out var address))
                {
                    errors.Add(new SyntaxError
                    {
                        LineNumber = lineNumber,
                        LineContent = rawLine,
                        Message = $"无效的IP地址: {ip}"
                    });
                    continue;
                }

                if (address.AddressFamily != AddressFamily.InterNetwork &&
                    address.AddressFamily != AddressFamily.InterNetworkV6)
                {
                    errors.Add(new SyntaxError
                    {
                        LineNumber = lineNumber,
                        LineContent = rawLine,
                        Message = $"不支持的IP地址类型: {ip}"
                    });
                    continue;
                }

                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    var bytes = address.GetAddressBytes();
                    if (bytes.All(b => b == 0))
                    {
                        errors.Add(new SyntaxError
                        {
                            LineNumber = lineNumber,
                            LineContent = rawLine,
                            Message = "0.0.0.0 不是有效的主机IP地址"
                        });
                        continue;
                    }
                    if (bytes.All(b => b == 255))
                    {
                        errors.Add(new SyntaxError
                        {
                            LineNumber = lineNumber,
                            LineContent = rawLine,
                            Message = "255.255.255.255 是广播地址，不能作为主机IP"
                        });
                        continue;
                    }
                }

                for (var j = 1; j < parts.Count; j++)
                {
                    var domain = parts[j];
                    var domainResult = ValidateDomainInternal(domain);
                    if (!domainResult.IsValid)
                    {
                        errors.Add(new SyntaxError
                        {
                            LineNumber = lineNumber,
                            LineContent = rawLine,
                            Message = $"无效的域名 \"{domain}\": {string.Join("; ", domainResult.Errors)}"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析hosts内容时发生错误");
        }

        return errors;
    }

    private static ValidationResult ValidateDomainInternal(string domain)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(domain))
        {
            errors.Add("域名不能为空");
            return ValidationResult.Fail(errors.ToArray());
        }

        if (domain.Length > 253)
        {
            errors.Add("域名过长");
            return ValidationResult.Fail(errors.ToArray());
        }

        var labels = domain.Split('.');
        foreach (var label in labels)
        {
            if (string.IsNullOrEmpty(label))
            {
                errors.Add("存在空标签段");
                continue;
            }
            if (label.Length > 63)
            {
                errors.Add($"标签 \"{label}\" 过长");
                continue;
            }
            if (!DomainLabelRegex().IsMatch(label))
            {
                errors.Add($"标签 \"{label}\" 格式不正确");
            }
        }

        return errors.Count > 0 ? ValidationResult.Fail(errors.ToArray()) : ValidationResult.Success();
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

    [GeneratedRegex(@"^[a-zA-Z0-9]([a-zA-Z0-9-]*[a-zA-Z0-9])?$")]
    private static partial Regex DomainLabelRegex();
}
