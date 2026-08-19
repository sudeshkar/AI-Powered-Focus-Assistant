using FocusAssistant.Appearance;
using System;
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace FocusAssistant.Converters
{
    /// <summary>
    /// Highlights whichever of the three theme buttons matches the current preference, the
    /// same way a segmented control would - Primary for the active choice, Secondary for the
    /// other two - without needing three separate boolean properties on the view model.
    /// </summary>
    public sealed class ThemePreferenceToAppearanceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AppThemePreference current && parameter is string target &&
                Enum.TryParse<AppThemePreference>(target, out var option))
            {
                return current == option ? ControlAppearance.Primary : ControlAppearance.Secondary;
            }

            return ControlAppearance.Secondary;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
