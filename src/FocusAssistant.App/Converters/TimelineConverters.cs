using FocusAssistant.Core.Reports;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FocusAssistant.Converters
{
    /// <summary>
    /// Colours a timeline segment by what was happening during it.
    /// </summary>
    /// <remarks>
    /// Untracked minutes are nearly invisible rather than a fourth colour. The strip is
    /// meant to show the shape of the day at a glance, and rendering "the app was not
    /// running" as prominently as "you were distracted" buries the signal in a bar that is
    /// mostly gaps.
    /// </remarks>
    public sealed class TimelineStateToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Productive = Frozen(0x2E, 0xA0, 0x43);
        private static readonly SolidColorBrush Distracting = Frozen(0xC0, 0x5B, 0x4A);
        private static readonly SolidColorBrush Idle = Frozen(0x80, 0x80, 0x80, 0x60);
        private static readonly SolidColorBrush Untracked = Frozen(0x80, 0x80, 0x80, 0x20);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is TimelineState state
                ? state switch
                {
                    TimelineState.Productive => Productive,
                    TimelineState.Distracting => Distracting,
                    TimelineState.Idle => Idle,
                    _ => Untracked,
                }
                : Untracked;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        private static SolidColorBrush Frozen(byte r, byte g, byte b, byte a = 0xFF)
        {
            var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
        }
    }

    /// <summary>
    /// Turns a segment's length in minutes into a proportional width.
    /// </summary>
    /// <remarks>
    /// The strip covers a fixed span of the day, so a minute is a fixed number of pixels
    /// and segments simply lay out side by side. A minimum of one pixel keeps very short
    /// runs visible - a two-minute distraction inside a long focused block is exactly the
    /// detail worth seeing.
    /// </remarks>
    public sealed class MinutesToWidthConverter : IValueConverter
    {
        /// <summary>Pixels per minute, tuned so an 18-hour strip fits a typical window.</summary>
        private const double PixelsPerMinute = 0.9;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is int minutes ? Math.Max(1, minutes * PixelsPerMinute) : 1d;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>Scales a 0-1 share to a bar width in pixels.</summary>
    public sealed class ShareToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var share = value is double d ? d : 0;
            var full = parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p)
                ? p
                : 240;

            return Math.Max(2, share * full);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>Green for productive applications, muted red for distracting ones.</summary>
    public sealed class ProductiveToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Productive = Frozen(0x2E, 0xA0, 0x43);
        private static readonly SolidColorBrush Distracting = Frozen(0xC0, 0x5B, 0x4A);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? Productive : Distracting;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        private static SolidColorBrush Frozen(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
