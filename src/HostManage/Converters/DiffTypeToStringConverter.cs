using System.Globalization;
using System.Windows.Data;
using HostManage.Services;

namespace HostManage.Converters;

public class DiffTypeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is DiffType type)
            {
                return type switch
                {
                    DiffType.Added => "新增",
                    DiffType.Removed => "删除",
                    DiffType.Modified => "修改",
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
