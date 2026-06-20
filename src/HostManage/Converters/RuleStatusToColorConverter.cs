using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using HostManage.Models;

namespace HostManage.Converters;

public class RuleStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is RuleStatus status)
            {
                return status switch
                {
                    RuleStatus.Active => new SolidColorBrush(Colors.Green),
                    RuleStatus.Inactive => new SolidColorBrush(Colors.Gray),
                    RuleStatus.Conflict => new SolidColorBrush(Colors.Orange),
                    RuleStatus.Invalid => new SolidColorBrush(Colors.Red),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }

            return new SolidColorBrush(Colors.Transparent);
        }
        catch
        {
            return new SolidColorBrush(Colors.Transparent);
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
