using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AstroPlanner.Converters;

namespace AstroPlanner.Controls;

/// <summary>
/// Draws 12 vertical colored bars (Jan–Dec) proportional to monthly observability scores.
/// </summary>
public class MonthScoreBarsControl : Control
{
    private static readonly string[] MonthAbbr = ["J","F","M","A","M","J","J","A","S","O","N","D"];

    public static readonly StyledProperty<double[]?> ScoresProperty =
        AvaloniaProperty.Register<MonthScoreBarsControl, double[]?>(nameof(Scores));

    public double[]? Scores
    {
        get => GetValue(ScoresProperty);
        set => SetValue(ScoresProperty, value);
    }

    static MonthScoreBarsControl()
    {
        AffectsRender<MonthScoreBarsControl>(ScoresProperty);
    }

    public override void Render(DrawingContext ctx)
    {
        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w < 12 || h < 10) return;

        const double labelH = 14;
        double barAreaH = h - labelH;
        double gap = 2;
        double barW = (w - 11 * gap) / 12.0;

        var scores = Scores;
        var textBrush = new SolidColorBrush(Color.FromRgb(110, 110, 125));
        var typeface = new Typeface("default");

        for (int i = 0; i < 12; i++)
        {
            double score = scores != null ? scores[i] : 0;
            double x = i * (barW + gap);

            var color = ScoreToColorConverter.ScoreToColor(score);
            double barH = score > 0 ? Math.Max(score / 100.0 * barAreaH, 3) : 2;
            double barY = barAreaH - barH;
            ctx.FillRectangle(new SolidColorBrush(color), new Rect(x, barY, barW, barH));

            var label = new FormattedText(
                MonthAbbr[i],
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface, 9, textBrush);
            ctx.DrawText(label, new Point(x + barW / 2 - label.Width / 2, barAreaH + 2));
        }
    }
}
