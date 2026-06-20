using System.Globalization;
using System.Windows.Data;

namespace HostManage.Converters;

public class NullToBoolConverter : IValueConverter
{
    public bool IsInverted { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            bool result = value != null;
            return IsInverted ? !result : result;
        }
        catch
        {
            return IsInverted;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
