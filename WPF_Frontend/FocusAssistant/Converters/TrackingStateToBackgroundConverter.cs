using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace FocusAssistant.Converters
{
    public class TrackingStateToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isTracking && isTracking)
                return new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red for Stop
            return new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green for Start
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
