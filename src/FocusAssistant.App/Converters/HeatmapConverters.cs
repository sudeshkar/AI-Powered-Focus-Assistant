using FocusAssistant.Core.Reports;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FocusAssistant.Converters
{
    /// <summary>
    /// Shades an hour of the day by how productive it tends to be.
    /// </summary>
    /// <remarks>
    /// Bound to the whole <see cref="HourlyFocus"/> record rather than just its productive
    /// share, because an hour with zero recorded minutes and an hour that was tracked and
    /// went entirely badly both have a share of zero - and those are very different facts.
    /// An untracked hour renders as a faint neutral outline instead of the coldest colour
    /// on the scale, so "nobody was at the keyboard at 3am" does not read as "3am goes
    /// badly".
    /// </remarks>
    public sealed class HourlyFocusToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Untracked = Frozen(Color.FromArgb(0x18, 0x80, 0x80, 0x80));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not HourlyFocus hour || hour.TotalMinutes <= 0)
                return Untracked;

            var brush = new SolidColorBrush(Color.FromArgb(
                (byte)Math.Clamp(40 + hour.ProductiveShare * 180, 40, 220),
                0x2E, 0xA0, 0x43));
            brush.Freeze();
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        private static SolidColorBrush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
