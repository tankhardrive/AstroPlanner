using System.Globalization;
using System.Text.Json;

namespace AstroPlanner.Services;

/// <summary>
/// Fetches Bortle-class sky darkness data from the lightpollutionmap.info unofficial API
/// and converts the radiance value to a Bortle class.
/// </summary>
public class LightPollutionService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>
    /// Fetches the Bortle class (1–9) for the given coordinates.
    /// Returns null if the fetch fails or coordinates are invalid.
    /// </summary>
    public async Task<int?> FetchBortleClassAsync(double lat, double lon)
    {
        var ic  = CultureInfo.InvariantCulture;
        var qd  = $"{{\"lng\":{lon.ToString("F4", ic)},\"lat\":{lat.ToString("F4", ic)}}}";
        var url = $"https://www.lightpollutionmap.info/QueryRaster/?ql=wa_2015&qt=point&qd={Uri.EscapeDataString(qd)}";

        try
        {
            var json = await Http.GetStringAsync(url);
            return ParseResponse(json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[LightPollution] Fetch failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static int? ParseResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            double radiance;

            // API may return {"data": value} or just the number directly
            if (root.TryGetProperty("data", out var dataProp))
                radiance = dataProp.GetDouble();
            else if (root.ValueKind == JsonValueKind.Number)
                radiance = root.GetDouble();
            else
                return null;

            return RadianceToBortle(radiance);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Converts radiance (mcd/m²) to Bortle class 1–9.</summary>
    private static int RadianceToBortle(double mcd)
    {
        if (mcd <  0.25) return 1;
        if (mcd <  0.50) return 2;
        if (mcd <  1.00) return 3;
        if (mcd <  3.00) return 4;
        if (mcd <  6.00) return 5;
        if (mcd < 12.00) return 6;
        if (mcd < 25.00) return 7;
        if (mcd < 50.00) return 8;
        return 9;
    }
}
