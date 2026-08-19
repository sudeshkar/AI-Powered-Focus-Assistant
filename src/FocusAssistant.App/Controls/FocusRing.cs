using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace FocusAssistant.Controls
{
    /// <summary>
    /// A circular gauge for the focus score.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Drawn rather than composed from primitives because WPF has no arc-by-percentage
    /// shape: an arc needs a start point, an end point computed by trigonometry, and the
    /// large-arc flag set once the sweep passes half the circle. Doing that in XAML means a
    /// converter per geometry, and doing it in a control means one <see cref="OnRender"/>.
    /// </para>
    /// <para>
    /// The colour is derived from the value rather than set by the caller so the same score
    /// always reads the same way on every screen. It shifts through amber rather than
    /// jumping straight to red: this number is a description of someone's day, and a
    /// scattered afternoon should look like information, not like a failure.
    /// </para>
    /// </remarks>
    public sealed class FocusRing : FrameworkElement
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(int), typeof(FocusRing),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
            nameof(Caption), typeof(string), typeof(FocusRing),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty HasDataProperty = DependencyProperty.Register(
            nameof(HasData), typeof(bool), typeof(FocusRing),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
            nameof(TrackBrush), typeof(Brush), typeof(FocusRing),
            new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
            nameof(Foreground), typeof(Brush), typeof(FocusRing),
            new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public string Caption
        {
            get => (string)GetValue(CaptionProperty);
            set => SetValue(CaptionProperty, value);
        }

        public bool HasData
        {
            get => (bool)GetValue(HasDataProperty);
            set => SetValue(HasDataProperty, value);
        }

        /// <summary>The unfilled part of the ring.</summary>
        public Brush TrackBrush
        {
            get => (Brush)GetValue(TrackBrushProperty);
            set => SetValue(TrackBrushProperty, value);
        }

        /// <summary>Text colour, taken from the theme by the hosting view.</summary>
        public Brush Foreground
        {
            get => (Brush)GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        protected override void OnRender(DrawingContext dc)
        {
            var size = Math.Min(ActualWidth, ActualHeight);
            if (size <= 0)
                return;

            const double thickness = 14;
            var centre = new Point(ActualWidth / 2, ActualHeight / 2);
            var radius = (size - thickness) / 2;

            var trackPen = new Pen(TrackBrush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawEllipse(null, trackPen, centre, radius, radius);

            var fraction = Math.Clamp(Value / 100.0, 0, 1);
            if (HasData && fraction > 0)
            {
                var pen = new Pen(ScoreBrush(Value), thickness)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                };

                dc.DrawGeometry(null, pen, BuildArc(centre, radius, fraction));
            }

            var numeral = HasData ? Value.ToString(CultureInfo.InvariantCulture) : "–";
            DrawCentred(dc, numeral, size * 0.30, FontWeights.SemiBold, centre, -size * 0.06);

            if (!string.IsNullOrEmpty(Caption))
                DrawCentred(dc, Caption, size * 0.10, FontWeights.Normal, centre, size * 0.14, 0.7);
        }

        /// <summary>
        /// An arc from twelve o'clock, clockwise. WPF measures angles from three o'clock and
        /// anticlockwise, hence the offset and the sign.
        /// </summary>
        private static Geometry BuildArc(Point centre, double radius, double fraction)
        {
            var startAngle = -Math.PI / 2;
            var sweep = fraction * 2 * Math.PI;
            var endAngle = startAngle + sweep;

            var start = new Point(centre.X + radius * Math.Cos(startAngle), centre.Y + radius * Math.Sin(startAngle));
            var end = new Point(centre.X + radius * Math.Cos(endAngle), centre.Y + radius * Math.Sin(endAngle));

            var figure = new PathFigure { StartPoint = start, IsClosed = false };
            figure.Segments.Add(new ArcSegment
            {
                Point = end,
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = sweep > Math.PI,
            });

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            geometry.Freeze();
            return geometry;
        }

        /// <summary>
        /// Green through amber to a muted red. Never alarming: this describes a day, and a
        /// scattered one is information rather than an error.
        /// </summary>
        private static Brush ScoreBrush(int value)
        {
            var brush = value switch
            {
                >= 80 => new SolidColorBrush(Color.FromRgb(0x2E, 0xA0, 0x43)),
                >= 60 => new SolidColorBrush(Color.FromRgb(0x5B, 0xA3, 0x00)),
                >= 40 => new SolidColorBrush(Color.FromRgb(0xC9, 0x8A, 0x00)),
                _ => new SolidColorBrush(Color.FromRgb(0xC0, 0x5B, 0x4A)),
            };

            brush.Freeze();
            return brush;
        }

        private void DrawCentred(
            DrawingContext dc, string text, double emSize, FontWeight weight,
            Point centre, double offsetY, double opacity = 1)
        {
            var brush = Foreground.Clone();
            brush.Opacity = opacity;
            brush.Freeze();

            var formatted = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"),
                    FontStyles.Normal, weight, FontStretches.Normal),
                emSize,
                brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(formatted, new Point(
                centre.X - formatted.Width / 2,
                centre.Y - formatted.Height / 2 + offsetY));
        }
    }
}
