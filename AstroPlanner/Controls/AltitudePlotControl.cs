using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AstroPlanner.Controls;

/// <summary>
/// Custom control that renders an altitude-over-night chart with a horizon line.
/// Binds to a list of (Time, ObjectAlt, HorizonAlt) samples.
/// </summary>
public class AltitudePlotControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<(DateTime Time, double Alt, double HorizAlt)>?> SamplesProperty =
        AvaloniaProperty.Register<AltitudePlotControl, IReadOnlyList<(DateTime, double, double)>?>(nameof(Samples));

    public static readonly StyledProperty<DateTime> DarkStartProperty =
        AvaloniaProperty.Register<AltitudePlotControl, DateTime>(nameof(DarkStart));

    public static readonly StyledProperty<DateTime> DarkEndProperty =
        AvaloniaProperty.Register<AltitudePlotControl, DateTime>(nameof(DarkEnd));

    public static readonly StyledProperty<TimeZoneInfo> TimeZoneProperty =
        AvaloniaProperty.Register<AltitudePlotControl, TimeZoneInfo>(nameof(TimeZone),
            defaultValue: TimeZoneInfo.Local);

    public IReadOnlyList<(DateTime Time, double Alt, double HorizAlt)>? Samples
    {
        get => GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    public DateTime DarkStart
    {
        get => GetValue(DarkStartProperty);
        set => SetValue(DarkStartProperty, value);
    }

    public DateTime DarkEnd
    {
        get => GetValue(DarkEndProperty);
        set => SetValue(DarkEndProperty, value);
    }

    public TimeZoneInfo TimeZone
    {
        get => GetValue(TimeZoneProperty);
        set => SetValue(TimeZoneProperty, value);
    }

    static AltitudePlotControl()
    {
        SamplesProperty.Changed.AddClassHandler<AltitudePlotControl>((c, _) => c.InvalidateVisual());
        TimeZoneProperty.Changed.AddClassHandler<AltitudePlotControl>((c, _) => c.InvalidateVisual());
    }

    public override void Render(DrawingContext ctx)
    {
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        if (bounds.Width < 20 || bounds.Height < 20) return;

        const double padL = 36, padR = 8, padT = 8, padB = 24;
        var plot = new Rect(padL, padT, bounds.Width - padL - padR, bounds.Height - padT - padB);

        // Background
        ctx.FillRectangle(new SolidColorBrush(Color.FromRgb(15, 15, 30)), bounds);

        var samples = Samples;
        if (samples == null || samples.Count < 2)
        {
            ctx.DrawText(
                new FormattedText("No data", System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, Typeface.Default, 12, Brushes.Gray),
                new Point(bounds.Width / 2 - 20, bounds.Height / 2 - 8));
            return;
        }

        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 0.5);
        var horizonPen = new Pen(new SolidColorBrush(Color.FromRgb(200, 100, 30)), 1.5);
        var altitudePen = new Pen(new SolidColorBrush(Color.FromRgb(80, 180, 255)), 2.0);
        var visiblePen = new Pen(new SolidColorBrush(Color.FromRgb(80, 255, 120)), 2.0);
        var axisPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 200, 200, 200)), 1.0);
        var textBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180));
        var typeface = new Typeface("monospace");

        DateTime tMin = samples[0].Time;
        DateTime tMax = samples[^1].Time;
        double totalSeconds = (tMax - tMin).TotalSeconds;
        const double altMin = 0, altMax = 90;

        Point ToPlot(DateTime t, double alt)
        {
            double x = plot.Left + (t - tMin).TotalSeconds / totalSeconds * plot.Width;
            double y = plot.Bottom - (alt - altMin) / (altMax - altMin) * plot.Height;
            return new Point(x, y);
        }

        // Draw altitude grid lines (0°, 30°, 60°, 90°)
        foreach (int alt in new[] { 0, 30, 60, 90 })
        {
            var p1 = ToPlot(tMin, alt);
            var p2 = ToPlot(tMax, alt);
            ctx.DrawLine(gridPen, p1, p2);

            var label = new FormattedText($"{alt}°",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 9, textBrush);
            ctx.DrawText(label, new Point(2, p1.Y - 6));
        }

        // Draw hour time axis (ticks are UTC; labels shown in site timezone)
        var tz = TimeZone;
        int startHour = tMin.Hour;
        for (int h = 0; h <= (int)(tMax - tMin).TotalHours + 1; h++)
        {
            var tickTime = tMin.Date.AddHours(startHour + h);
            if (tickTime < tMin || tickTime > tMax) continue;
            var p = ToPlot(tickTime, altMin);
            ctx.DrawLine(axisPen, p, new Point(p.X, p.Y + 5));
            var tickLocal = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(tickTime, DateTimeKind.Utc), tz);
            var label = new FormattedText(tickLocal.ToString("HH:mm"),
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 9, textBrush);
            ctx.DrawText(label, new Point(p.X - 12, plot.Bottom + 6));
        }

        // Draw axes
        ctx.DrawLine(axisPen, new Point(plot.Left, plot.Top), new Point(plot.Left, plot.Bottom));
        ctx.DrawLine(axisPen, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Bottom));

        // Draw horizon profile line
        var horizonGeom = new StreamGeometry();
        using (var gc = horizonGeom.Open())
        {
            bool first = true;
            foreach (var (t, _, horizAlt) in samples)
            {
                var p = ToPlot(t, Math.Max(0, horizAlt));
                if (first) { gc.BeginFigure(p, false); first = false; }
                else gc.LineTo(p);
            }
        }
        ctx.DrawGeometry(null, horizonPen, horizonGeom);

        // Draw object altitude, split into visible (green) and below-horizon (blue) segments
        var aboveGeom = new StreamGeometry();
        var belowGeom = new StreamGeometry();

        using (var above = aboveGeom.Open())
        using (var below = belowGeom.Open())
        {
            bool? wasAbove = null;

            for (int i = 0; i < samples.Count; i++)
            {
                var (t, alt, horizAlt) = samples[i];
                bool isAbove = alt > horizAlt;
                var p = ToPlot(t, Math.Max(0, alt));

                if (wasAbove == null)
                {
                    above.BeginFigure(p, false);
                    below.BeginFigure(p, false);
                }
                else if (isAbove != wasAbove)
                {
                    above.BeginFigure(p, false);
                    below.BeginFigure(p, false);
                }

                if (isAbove) above.LineTo(p);
                else below.LineTo(p);

                wasAbove = isAbove;
            }
        }

        ctx.DrawGeometry(null, altitudePen, belowGeom);
        ctx.DrawGeometry(null, visiblePen, aboveGeom);
    }
}
