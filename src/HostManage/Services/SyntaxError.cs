namespace HostManage.Services;

public class SyntaxError
{
    public int LineNumber { get; set; }

    public string Message { get; set; } = string.Empty;

    public string LineContent { get; set; } = string.Empty;
}
