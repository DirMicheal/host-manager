using System.Globalization;
using System.Windows.Data;
using HostManage.Models;

namespace HostManage.Converters;

public class LogLevelToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Info => "信息",
                    LogLevel.Warn => "警告",
                    LogLevel.Error => "错误",
                    LogLevel.Action => "操作",
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
