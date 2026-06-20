using System.Globalization;
using System.Windows.Data;
using HostManage.Models;

namespace HostManage.Converters;

public class RuleStatusToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is RuleStatus status)
            {
                return status switch
                {
                    RuleStatus.Active => "启用",
                    RuleStatus.Inactive => "禁用",
                    RuleStatus.Conflict => "冲突",
                    RuleStatus.Invalid => "无效",
                    _ => "未知"
                };
            }

            return "未知";
        }
        catch
        {
            return "未知";
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
