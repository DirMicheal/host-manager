namespace HostManage.Services;

public class ConflictInfo
{
    public string Domain { get; set; } = string.Empty;

    public List<Guid> ConflictingRuleIds { get; set; } = new();
}
