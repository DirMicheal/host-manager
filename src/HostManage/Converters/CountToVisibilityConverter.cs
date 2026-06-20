using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HostManage.Converters;

public class CountToVisibilityConverter : IValueConverter
{
    public bool IsInverted { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            bool hasItems = false;

            if (value is IEnumerable enumerable)
            {
                var enumerator = enumerable.GetEnumerator();
                hasItems = enumerator.MoveNext();
            }
            else if (value is int count)
            {
                hasItems = count > 0;
            }

            if (IsInverted)
                hasItems = !hasItems;

            return hasItems ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            return Visibility.Collapsed;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
