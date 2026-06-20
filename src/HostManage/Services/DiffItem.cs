namespace HostManage.Services;

public enum DiffType
{
    Added,
    Removed,
    Modified
}

public class DiffItem
{
    public DiffType Type { get; set; }

    public string Property { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }
}
