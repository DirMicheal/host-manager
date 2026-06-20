using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using HostManage.Models;

namespace HostManage.Converters;

public class LogLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Info => new SolidColorBrush(Colors.SteelBlue),
                    LogLevel.Warn => new SolidColorBrush(Colors.DarkOrange),
                    LogLevel.Error => new SolidColorBrush(Colors.Red),
                    LogLevel.Action => new SolidColorBrush(Colors.ForestGreen),
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
