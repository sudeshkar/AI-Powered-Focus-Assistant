using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FocusAssistant.Converters
{

    public class TrackingColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isTracking && isTracking)
                return new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
            return new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}