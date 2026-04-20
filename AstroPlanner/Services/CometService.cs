using System.Globalization;
using System.Text.Json;
using AstroPlanner.Models;

namespace AstroPlanner.Services;

public class CometService
{
    private static readonly string CacheFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AstroPlanner", "comets.json");

    private const string MpcUrl =
        "https://www.minorplanetcenter.net/iau/Ephemerides/Comets/Soft00Cmt.txt";

    private List<CometObject> _comets = [];

    public event Action? CometDataRefreshed;

    public IReadOnlyList<CometObject> GetAll() => _comets;

    public async Task LoadAsync()
    {
        if (File.Exists(CacheFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(CacheFile);
                var cached = JsonSerializer.Deserialize<List<CometObject>>(json);
                if (cached != null && cached.Count > 0)
                {
                    _comets = cached;
                    return;
                }
            }
            catch { /* corrupt cache — fall through to fetch */ }
        }
        await RefreshAsync();
    }

    /// <summary>
    /// Fetches fresh orbital elements from MPC. Returns null on success, or an error message.
    /// </summary>
    public async Task<string?> RefreshAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var text = await http.GetStringAsync(MpcUrl);
            var comets = ParseSoft00(text);
            if (comets.Count > 0)
            {
                _comets = comets;
                Directory.CreateDirectory(Path.GetDirectoryName(CacheFile)!);
                await File.WriteAllTextAsync(CacheFile, JsonSerializer.Serialize(_comets));
                CometDataRefreshed?.Invoke();
            }
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    // Soft00Cmt.txt single-line format:
    // <designation> <year> <month> <day.frac> <q> <e> <peri> <node> <incl> <epoch> <H> <G> <name> [reference]
    // Example:
    //   CJ95O010  1997 03 29.0259  0.920471  0.994909  130.6845  281.7473  89.7644  20260419  -2.0  4.0  C/1995 O1 (Hale-Bopp)  MPEC 2026-FC3
    private static List<CometObject> ParseSoft00(string text)
    {
        var comets = new List<CometObject>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 12) continue;

            // parts[0]=designation, [1]=year, [2]=month, [3]=day, [4]=q, [5]=e,
            // [6]=peri, [7]=node, [8]=incl, [9]=epoch(YYYYMMDD), [10]=H, [11]=G, [12+]=name [ref]
            if (!int.TryParse(parts[1], out int year) || year is < 1900 or > 2060) continue;
            if (!int.TryParse(parts[2], out int month) || month is < 1 or > 12) continue;

            var comet = TryParseLine(parts, year, month);
            if (comet != null) comets.Add(comet);
        }
        return comets;
    }

    private static CometObject? TryParseLine(string[] parts, int year, int month)
    {
        try
        {
            if (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double dayDecimal)) return null;
            if (!double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double q)) return null;
            if (!double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out double e)) return null;
            if (!double.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out double argPeri)) return null;
            if (!double.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out double longNode)) return null;
            if (!double.TryParse(parts[8], NumberStyles.Float, CultureInfo.InvariantCulture, out double incl)) return null;
            // parts[9] = epoch (YYYYMMDD integer, skip)
            double? h = double.TryParse(parts[10], NumberStyles.Float, CultureInfo.InvariantCulture, out double hv) ? hv : null;
            double? g = double.TryParse(parts[11], NumberStyles.Float, CultureInfo.InvariantCulture, out double gv) ? gv : null;

            int intDay = Math.Clamp((int)dayDecimal, 1, DateTime.DaysInMonth(year, month));
            double dayFrac = dayDecimal - intDay;
            var periDate = new DateTime(year, month, intDay, 0, 0, 0, DateTimeKind.Utc).AddDays(dayFrac);
            double tp = AstronomyService.ToJulianDay(periDate);

            // Name: parts[12..] until trailing reference token (MPEC/MPC/unp)
            int nameEnd = parts.Length;
            for (int i = 12; i < parts.Length; i++)
            {
                if (parts[i] is "MPEC" or "MPC" or "unp") { nameEnd = i; break; }
            }
            string name = nameEnd > 12
                ? string.Join(" ", parts[12..nameEnd]).Trim()
                : parts[0];

            return new CometObject
            {
                Designation = parts[0],
                Name = name,
                PerihelionDistanceAu = q,
                Eccentricity = e,
                ArgPerihelionDeg = argPeri,
                LongAscNodeDeg = longNode,
                InclinationDeg = incl,
                PerihelionJd = tp,
                MagnitudeH = h,
                MagnitudeG = g,
            };
        }
        catch { return null; }
    }
}
