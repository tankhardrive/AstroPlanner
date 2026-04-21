using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AstroPlanner.Converters;

public class ScoreToColorConverter : IValueConverter
{
    public static readonly ScoreToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double score = value is double d ? d : 0;
        return new SolidColorBrush(ScoreToColor(score));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();

    internal static Color ScoreToColor(double score) => score switch
    {
        <= 0          => Color.FromRgb(32, 32, 42),
        < 25          => Color.FromRgb(140, 48, 48),
        < 50          => Color.FromRgb(185, 122, 32),
        < 70          => Color.FromRgb(162, 178, 42),
        _             => Color.FromRgb(48, 178, 68),
    };
}
