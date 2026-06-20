using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using HostManage.Services;

namespace HostManage.Converters;

public class DiffTypeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is DiffType type)
            {
                return type switch
                {
                    DiffType.Added => new SolidColorBrush(Colors.Green),
                    DiffType.Removed => new SolidColorBrush(Colors.Red),
                    DiffType.Modified => new SolidColorBrush(Colors.Orange),
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
