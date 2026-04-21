using System.Globalization;
using Avalonia.Data.Converters;

namespace AstroPlanner.Converters;

public class ScoreToDisplayConverter : IValueConverter
{
    public static readonly ScoreToDisplayConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double score || score <= 0) return "—";
        return $"{score:F0}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
