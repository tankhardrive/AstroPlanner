using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AstroPlanner.Models;
using AstroPlanner.ViewModels;

namespace AstroPlanner.Controls;

/// <summary>
/// Draws a heatmap-style chart where each row is an observing metric and each column is an hour.
/// Cells are colored from red (bad) to green (good) per metric's own scale.
/// </summary>
public class WeatherChartControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<WeatherHour>?> HoursProperty =
        AvaloniaProperty.Register<WeatherChartControl, IReadOnlyList<WeatherHour>?>(nameof(Hours));

    public static readonly StyledProperty<TimeZoneInfo> TimeZoneProperty =
        AvaloniaProperty.Register<WeatherChartControl, TimeZoneInfo>(nameof(TimeZone),
            defaultValue: TimeZoneInfo.Local);

    public IReadOnlyList<WeatherHour>? Hours
    {
        get => GetValue(HoursProperty);
        set => SetValue(HoursProperty, value);
    }
    public TimeZoneInfo TimeZone
    {
        get => GetValue(TimeZoneProperty);
        set => SetValue(TimeZoneProperty, value);
    }

    static WeatherChartControl()
    {
        HoursProperty.Changed.AddClassHandler<WeatherChartControl>((c, _) => c.InvalidateVisual());
        TimeZoneProperty.Changed.AddClassHandler<WeatherChartControl>((c, _) => c.InvalidateVisual());
    }

    // ── Layout constants ──────────────────────────────────────────────────────
    private const double LabelW  = 105;
    private const double CellH   = 26;
    private const double RowGap  = 4;
    private const double PadTop  = 24;   // header row for time labels
    private const double PadLeft = 8;
    private const double PadBot  = 8;

    private static readonly (string Label, Func<WeatherHour, double?> Value, Func<double, Color> ColorMap)[] Rows =
    [
        ("Score",        h => h.Score,
            v => WeatherHourViewModel.ScoreToColor((int)Math.Round(v))),

        ("Cloud Cover",  h => h.CloudCoverPercent,
            v => Lerp(Color.FromRgb(60, 180, 255), Color.FromRgb(30, 30, 70), v / 100.0)),

        ("Seeing (1-8)", h => h.Seeing,
            v => Lerp(Color.FromRgb(50, 200, 80), Color.FromRgb(220, 50, 50), (v - 1) / 7.0)),

        ("Transparency", h => h.Transparency,
            v => Lerp(Color.FromRgb(220, 50, 50), Color.FromRgb(50, 200, 80), (v - 1) / 7.0)),

        ("Humidity",     h => h.HumidityPercent,
            v => Lerp(Color.FromRgb(50, 200, 80), Color.FromRgb(220, 130, 50), v / 100.0)),

        ("Precip Prob",  h => h.PrecipProbabilityPercent,
            v => Lerp(Color.FromRgb(50, 200, 80), Color.FromRgb(30, 80, 200), v / 100.0)),

        ("Wind",         h => h.WindSpeedMph,
            v => Lerp(Color.FromRgb(50, 200, 80), Color.FromRgb(220, 50, 50), v / 40.0)),
    ];

    public override void Render(DrawingContext ctx)
    {
        var bounds   = new Rect(0, 0, Bounds.Width, Bounds.Height);
        var hours    = Hours;
        var typeface = new Typeface("monospace");
        var labelBrush = new SolidColorBrush(Color.FromRgb(180, 180, 200));
        var dimBrush   = new SolidColorBrush(Color.FromRgb(100, 100, 115));
        var nullCell   = new SolidColorBrush(Color.FromRgb(45, 45, 58));

        // Background
        ctx.FillRectangle(new SolidColorBrush(Color.FromRgb(12, 12, 22)), bounds);

        if (hours == null || hours.Count == 0)
        {
            var ft = new FormattedText("No data", System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 12, dimBrush);
            ctx.DrawText(ft, new Point((bounds.Width - ft.Width) / 2, (bounds.Height - ft.Height) / 2));
            return;
        }

        int n = hours.Count;
        double cellW = Math.Max(30, (bounds.Width - PadLeft - LabelW) / n);
        var tz = TimeZone;

        // ── Hour labels (header row) ──────────────────────────────────────────
        for (int i = 0; i < n; i++)
        {
            double cx = PadLeft + LabelW + i * cellW + cellW / 2;
            var local = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(hours[i].UtcTime, DateTimeKind.Utc), tz);
            string label = local.ToString("h tt");
            var ft = new FormattedText(label,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 9, dimBrush);
            ctx.DrawText(ft, new Point(cx - ft.Width / 2, 4));
        }

        // ── Metric rows ───────────────────────────────────────────────────────
        for (int row = 0; row < Rows.Length; row++)
        {
            var (label, getValue, colorMap) = Rows[row];
            double rowY = PadTop + row * (CellH + RowGap);

            // Row label
            var labelFt = new FormattedText(label,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 10, labelBrush);
            ctx.DrawText(labelFt, new Point(PadLeft, rowY + (CellH - labelFt.Height) / 2));

            // Cells
            for (int i = 0; i < n; i++)
            {
                double cx = PadLeft + LabelW + i * cellW;
                var cellRect = new Rect(cx + 1, rowY, cellW - 2, CellH);

                double? val = getValue(hours[i]);
                IBrush fill;
                if (val.HasValue)
                {
                    var c = colorMap(Math.Clamp(val.Value, 0, 1000));
                    fill = new SolidColorBrush(c);
                }
                else
                {
                    fill = nullCell;
                }

                ctx.FillRectangle(fill, cellRect, 3);
            }
        }
    }

    // ── Color helpers ─────────────────────────────────────────────────────────

    private static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }
}
