using System.Globalization;
using System.Windows.Data;

namespace HostManage.Converters;

public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value == null || parameter == null)
                return false;

            string? valueString = value.ToString();
            string? parameterString = parameter.ToString();

            if (string.IsNullOrEmpty(valueString) || string.IsNullOrEmpty(parameterString))
                return false;

            if (Enum.IsDefined(value.GetType(), value) == false)
                return false;

            object? parameterValue = Enum.Parse(value.GetType(), parameterString, true);
            return value.Equals(parameterValue);
        }
        catch
        {
            return false;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is bool boolValue && boolValue && parameter != null)
            {
                string? parameterString = parameter.ToString();
                if (!string.IsNullOrEmpty(parameterString))
                {
                    return Enum.Parse(targetType, parameterString, true);
                }
            }

            return Binding.DoNothing;
        }
        catch
        {
            return Binding.DoNothing;
        }
    }
}
