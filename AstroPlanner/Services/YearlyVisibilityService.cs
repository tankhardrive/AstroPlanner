using AstroPlanner.Models;
using AstroPlanner.ViewModels;

namespace AstroPlanner.Services;

public class YearlyVisibilityService
{
    private readonly VisibilityService _visibility;

    public YearlyVisibilityService(VisibilityService visibility)
    {
        _visibility = visibility;
    }

    public async Task ComputeAsync(
        IReadOnlyList<ObjectRowViewModel> dsoRows,
        ObservationSite site,
        HorizonProfile horizon,
        int year,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        // Precompute moon info once per month (midnight at the midpoint of darkness)
        var moonInfos = new (double Az, double Alt, double Illum)[12];
        for (int m = 0; m < 12; m++)
        {
            ct.ThrowIfCancellationRequested();
            var date = new DateOnly(year, m + 1, 15);
            var (darkStart, darkEnd) = AstronomyService.GetAstronomicalDarkness(date, site);
            var midNight = darkStart + (darkEnd - darkStart) / 2;
            moonInfos[m] = AstronomyService.GetMoonPosition(midNight);
        }

        int total = dsoRows.Count * 12;
        int done = 0;

        await Task.Run(() =>
        {
            var opts = new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct };

            Parallel.ForEach(dsoRows, opts, row =>
            {
                if (row.DsoSource == null) return;

                var scores = new double[12];
                for (int m = 0; m < 12; m++)
                {
                    var date = new DateOnly(year, m + 1, 15);
                    var vis = _visibility.ComputeDso(
                        row.DsoSource, date, site, horizon, stepMinutes: 30, moonInfos[m]);

                    if (vis.IsVisible)
                        scores[m] = ComputeScore(row, vis);

                    int d = Interlocked.Increment(ref done);
                    if (d % 1000 == 0)
                        progress?.Report((double)d / total * 100);
                }

                row.MonthlyScores = scores;
                row.NotifyYearScoresChanged();
            });
        }, ct);

        progress?.Report(100);
    }

    internal static double ComputeScore(ObjectRowViewModel row, VisibilityWindow vis)
    {
        double frac   = vis.VisibilityFraction * 15;
        double dur    = Math.Clamp(vis.Duration.TotalHours / 5.0, 0, 1) * 10;
        double alt    = Math.Min(vis.AverageAltitudeDegrees / 45.0, 1.0) * 20;
        double moon   = Math.Min(vis.MoonSeparationDegrees / 90.0, 1.0) * 20;
        double? mag   = row.Magnitude;
        double bright = mag.HasValue ? Math.Clamp((15.0 - mag.Value) / 15.0, 0, 1) * 15 : 7.5;
        double? arcmin = row.DsoSource?.MajorAxisArcmin;
        double size   = arcmin is double s && s > 0
            ? Math.Clamp(Math.Log10(Math.Max(s, 1)) / Math.Log10(30), 0, 1) * 10 : 0;
        double peakDeg = vis.PeakAltitudeDegrees;
        double peak   = peakDeg < 10 ? 0
            : peakDeg < 20 ? (peakDeg - 10) / 10.0 * 3
            : Math.Min((peakDeg - 20) / 70.0, 1.0) * 7 + 3;
        return Math.Round(Math.Max(frac + dur + alt + moon + bright + size + peak, 0), 1);
    }
}
