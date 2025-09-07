using System;
using System.Globalization;
using System.Windows.Data;

namespace FocusAssistant.Converters
{
    public class TrackingButtonContentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isTracking)
            {
                return isTracking ? "⏹️ Stop Tracking" : "▶️ Start Tracking";
            }
            return "▶️ Start Tracking";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}