using System.Globalization;

namespace AstroPlanner.Services;

public record ImageSourceInfo(string Key, string Label, string Credit);

/// <summary>
/// Fetches sky survey images for a given RA/Dec from multiple sources.
/// Results are cached in memory for the session.
/// </summary>
public class ImageService
{
    private static readonly HttpClient Http = CreateHttpClient();
    private readonly Dictionary<string, byte[]> _cache = new();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AstroPlanner/1.0");
        return client;
    }

    public static readonly IReadOnlyList<ImageSourceInfo> Sources =
    [
        new("dss",    "DSS2 Red",     "DSS2 Red · NASA SkyView"),
        new("legacy", "Color",        "DESI Legacy Survey DR10 · legacysurvey.org"),
    ];

    public async Task<byte[]?> FetchAsync(double raDeg, double decDeg, double sizeDeg, string sourceKey = "dss")
    {
        string cacheKey = $"{sourceKey}:{raDeg:F4},{decDeg:F4},{sizeDeg:F3}";
        if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

        try
        {
            string url = sourceKey switch
            {
                "legacy" => BuildLegacyUrl(raDeg, decDeg, sizeDeg),
                _        => BuildDssUrl(raDeg, decDeg, sizeDeg),
            };

            var bytes = await Http.GetByteArrayAsync(url);
            _cache[cacheKey] = bytes;
            return bytes;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ImageService] {sourceKey} fetch failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static string BuildDssUrl(double ra, double dec, double sizeDeg)
    {
        var ic = CultureInfo.InvariantCulture;
        return "https://skyview.gsfc.nasa.gov/current/cgi/runquery.pl" +
               $"?Survey=DSS2+Red" +
               $"&Position={ra.ToString("F6", ic)},{dec.ToString("F6", ic)}" +
               $"&Size={sizeDeg.ToString("F4", ic)}" +
               $"&Pixels=400&Return=JPEG&Catalog=none&Sampler=LI";
    }

    private static string BuildLegacyUrl(double ra, double dec, double sizeDeg)
    {
        var ic = CultureInfo.InvariantCulture;
        // pixscale: arcsec/pixel so the full field fits in 512px
        double pixscale = Math.Max(sizeDeg * 3600.0 / 512.0, 0.262);
        return "https://www.legacysurvey.org/viewer/jpeg-cutout" +
               $"?ra={ra.ToString("F6", ic)}" +
               $"&dec={dec.ToString("F6", ic)}" +
               $"&size=512" +
               $"&layer=ls-dr10" +
               $"&pixscale={pixscale.ToString("F4", ic)}";
    }
}
