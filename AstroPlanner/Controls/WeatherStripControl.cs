using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AstroPlanner.Models;
using AstroPlanner.ViewModels;

namespace AstroPlanner.Controls;

/// <summary>
/// A thin horizontal strip showing a smooth score-based gradient across the night.
/// Green = good observing conditions, red = poor. Click to open the detail window.
/// </summary>
public class WeatherStripControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<WeatherHour>?> HoursProperty =
        AvaloniaProperty.Register<WeatherStripControl, IReadOnlyList<WeatherHour>?>(nameof(Hours));

    public static readonly StyledProperty<DateTime> DarkStartProperty =
        AvaloniaProperty.Register<WeatherStripControl, DateTime>(nameof(DarkStart));

    public static readonly StyledProperty<DateTime> DarkEndProperty =
        AvaloniaProperty.Register<WeatherStripControl, DateTime>(nameof(DarkEnd));

    public static readonly StyledProperty<TimeZoneInfo> TimeZoneProperty =
        AvaloniaProperty.Register<WeatherStripControl, TimeZoneInfo>(nameof(TimeZone),
            defaultValue: TimeZoneInfo.Local);

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<WeatherStripControl, bool>(nameof(IsLoading));

    public static readonly StyledProperty<bool> HasDataProperty =
        AvaloniaProperty.Register<WeatherStripControl, bool>(nameof(HasData));

    public static readonly StyledProperty<string> StatusMessageProperty =
        AvaloniaProperty.Register<WeatherStripControl, string>(nameof(StatusMessage), defaultValue: "");

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<WeatherStripControl, ICommand?>(nameof(Command));

    public IReadOnlyList<WeatherHour>? Hours
    {
        get => GetValue(HoursProperty);
        set => SetValue(HoursProperty, value);
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
    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }
    public bool HasData
    {
        get => GetValue(HasDataProperty);
        set => SetValue(HasDataProperty, value);
    }
    public string StatusMessage
    {
        get => GetValue(StatusMessageProperty);
        set => SetValue(StatusMessageProperty, value);
    }
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    static WeatherStripControl()
    {
        HoursProperty.Changed.AddClassHandler<WeatherStripControl>((c, _) => c.InvalidateVisual());
        IsLoadingProperty.Changed.AddClassHandler<WeatherStripControl>((c, _) => c.InvalidateVisual());
        HasDataProperty.Changed.AddClassHandler<WeatherStripControl>((c, _) => c.InvalidateVisual());
        StatusMessageProperty.Changed.AddClassHandler<WeatherStripControl>((c, _) => c.InvalidateVisual());
        TimeZoneProperty.Changed.AddClassHandler<WeatherStripControl>((c, _) => c.InvalidateVisual());
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && Command?.CanExecute(null) == true)
            Command.Execute(null);
    }

    public override void Render(DrawingContext ctx)
    {
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        if (bounds.Width < 10 || bounds.Height < 10) return;

        // Background
        var bg = new SolidColorBrush(Color.FromRgb(12, 12, 22));
        ctx.FillRectangle(bg, bounds);

        // Top separator line
        ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(80, 200, 200, 200)), 1),
            new Point(0, 0), new Point(bounds.Width, 0));

        const double padL = 8, padR = 8, padTop = 6, labelAreaH = 16;
        double barH    = bounds.Height - padTop - labelAreaH - 4;
        var    barRect = new Rect(padL, padTop, bounds.Width - padL - padR, barH);

        var textBrush = new SolidColorBrush(Color.FromRgb(160, 160, 180));
        var typeface  = new Typeface("monospace");

        var hours = Hours;

        // ── No data / loading state ────────────────────────────────────────────
        if (IsLoading || hours == null || hours.Count == 0)
        {
            string msg = IsLoading ? "Loading weather…" : (StatusMessage.Length > 0 ? StatusMessage : "No weather data");
            ctx.FillRectangle(new SolidColorBrush(Color.FromRgb(28, 28, 45)), barRect);
            var ft = new FormattedText(msg,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 11, textBrush);
            ctx.DrawText(ft, new Point(padL + (barRect.Width - ft.Width) / 2, padTop + (barH - ft.Height) / 2));
            return;
        }

        // ── Gradient bar ──────────────────────────────────────────────────────
        var stops = new GradientStops();
        for (int i = 0; i < hours.Count; i++)
        {
            double offset = hours.Count > 1 ? (double)i / (hours.Count - 1) : 0.5;
            stops.Add(new GradientStop(WeatherHourViewModel.ScoreToColor(hours[i].Score), offset));
        }
        var gradBrush = new LinearGradientBrush
        {
            StartPoint    = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint      = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops = stops,
        };
        ctx.FillRectangle(gradBrush, barRect);

        // Subtle rounded overlay to add depth (draw a translucent white glint at top)
        var glintRect = new Rect(barRect.X, barRect.Y, barRect.Width, barH * 0.35);
        ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)), glintRect);

        // ── Hour tick labels ──────────────────────────────────────────────────
        var tz         = TimeZone;
        double labelY  = barRect.Bottom + 3;

        for (int i = 0; i < hours.Count; i++)
        {
            double offset = hours.Count > 1 ? (double)i / (hours.Count - 1) : 0.5;
            double x      = padL + offset * barRect.Width;

            var local = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(hours[i].UtcTime, DateTimeKind.Utc), tz);
            string label = local.ToString("h tt");

            var ft = new FormattedText(label,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 9, textBrush);

            // Avoid clipping the last label
            double drawX = Math.Min(x - ft.Width / 2, bounds.Width - padR - ft.Width);
            drawX = Math.Max(drawX, padL);
            ctx.DrawText(ft, new Point(drawX, labelY));

            // Tick mark
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(100, 200, 200, 200)), 1),
                new Point(x, barRect.Bottom), new Point(x, barRect.Bottom + 3));
        }

        // ── "Click for details" hint on hover (always visible at right edge) ──
        const string hint = "click for details ›";
        var hintFt = new FormattedText(hint,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, 9,
            new SolidColorBrush(Color.FromArgb(90, 200, 200, 220)));
        ctx.DrawText(hintFt, new Point(bounds.Width - padR - hintFt.Width, padTop + (barH - hintFt.Height) / 2));
    }
}
