using System.Globalization;
using System.Windows.Data;

namespace HostManage.Converters;

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }

            return true;
        }
        catch
        {
            return true;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
