using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FocusAssistant.Converters
{
    public class IdleStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.ToString().Contains("Idle") ? Brushes.Orange : Brushes.Green;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
