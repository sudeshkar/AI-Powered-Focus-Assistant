using System;
using System.Globalization;
using System.Windows.Data;

namespace FocusAssistant.Converters
{
    public class StringToDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && double.TryParse(str.TrimEnd('%'), out double result))
            {
                return result;
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}