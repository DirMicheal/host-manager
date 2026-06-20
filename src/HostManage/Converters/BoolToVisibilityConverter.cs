using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HostManage.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool IsInverted { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is bool boolValue)
            {
                if (IsInverted)
                    boolValue = !boolValue;

                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }
        catch
        {
            return Visibility.Collapsed;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is Visibility visibility)
            {
                bool result = visibility == Visibility.Visible;
                return IsInverted ? !result : result;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
