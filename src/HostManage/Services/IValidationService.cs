using HostManage.Models;

namespace HostManage.Services;

public interface IValidationService
{
    ValidationResult ValidateHostRule(HostRule rule);

    ValidationResult ValidateIP(string ip);

    ValidationResult ValidateDomain(string domain);

    List<ConflictInfo> ScanConflicts(IEnumerable<HostRule> rules);

    List<SyntaxError> ParseHostsContent(string content);
}
