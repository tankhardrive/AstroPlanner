using AstroPlanner.Models;

namespace AstroPlanner.Services;

/// <summary>
/// Computes horizon-aware visibility windows for celestial objects over a single night.
/// </summary>
public class VisibilityService
{
    private readonly AstronomyService _astronomy = new();

    public VisibilityWindow ComputeDso(
        DeepSkyObject obj,
        DateOnly observingDate,
        ObservationSite site,
        HorizonProfile horizon,
        int stepMinutes,
        (double RaDeg, double DecDeg, double IllumPct) moonInfo)
    {
        return Compute(
            _ => (obj.RaDegrees, obj.DecDegrees),
            observingDate, site, horizon, stepMinutes, moonInfo);
    }

    public VisibilityWindow ComputeSolarSystem(
        SolarSystemObject obj,
        DateOnly observingDate,
        ObservationSite site,
        HorizonProfile horizon,
        int stepMinutes,
        (double RaDeg, double DecDeg, double IllumPct) moonInfo)
    {
        return Compute(
            t => AstronomyService.GetPlanetPosition(obj.BodyType, t),
            observingDate, site, horizon, stepMinutes, moonInfo);
    }

    public VisibilityWindow ComputeComet(
        CometObject comet,
        DateOnly observingDate,
        ObservationSite site,
        HorizonProfile horizon,
        int stepMinutes,
        (double RaDeg, double DecDeg, double IllumPct) moonInfo)
    {
        return Compute(
            t => AstronomyService.GetCometPosition(comet, t),
            observingDate, site, horizon, stepMinutes, moonInfo);
    }

    private VisibilityWindow Compute(
        Func<DateTime, (double Ra, double Dec)> getPosition,
        DateOnly observingDate,
        ObservationSite site,
        HorizonProfile horizon,
        int stepMinutes,
        (double RaDeg, double DecDeg, double IllumPct) moonInfo)
    {
        var (darkStart, darkEnd) = AstronomyService.GetAstronomicalDarkness(observingDate, site);

        if (darkEnd <= darkStart)
            return VisibilityWindow.NeverVisible;

        double totalDarkMinutes = (darkEnd - darkStart).TotalMinutes;
        double visibleMinutes = 0;
        double peakAlt = double.MinValue;
        double peakAz = 0;
        double peakClearance = double.MinValue;
        DateTime peakTime = darkStart;
        double peakRa = 0, peakDec = 0;
        double visibleAltSum = 0;
        int visibleAltCount = 0;

        var steps = new List<(DateTime Time, double Alt, double HorizAlt)>();
        var t = darkStart;

        while (t <= darkEnd)
        {
            var (ra, dec) = getPosition(t);
            var (alt, az) = AstronomyService.EquatorialToHorizontal(ra, dec, t,
                site.LatitudeDegrees, site.LongitudeDegrees);
            double horizAlt = horizon.GetAltitudeAt(az);

            steps.Add((t, alt, horizAlt));

            if (alt > horizAlt)
            {
                visibleMinutes += stepMinutes;
                visibleAltSum += alt;
                visibleAltCount++;
                double clearance = alt - horizAlt;
                if (clearance > peakClearance || alt > peakAlt)
                {
                    if (alt > peakAlt)
                    {
                        peakAlt = alt;
                        peakAz = az;
                        peakTime = t;
                        peakRa = ra;
                        peakDec = dec;
                    }
                    peakClearance = Math.Max(peakClearance, clearance);
                }
            }

            t = t.AddMinutes(stepMinutes);
        }

        if (visibleMinutes <= 0)
            return VisibilityWindow.NeverVisible;

        double moonSep = AstronomyService.AngularSeparationDeg(
            peakRa, peakDec, moonInfo.RaDeg, moonInfo.DecDeg);

        DateTime? riseTime = null, setTime = null;
        bool riseDetected = false;

        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i].Alt > steps[i].HorizAlt)
            {
                if (!riseDetected)
                {
                    riseDetected = true;
                    riseTime = steps[i].Time == darkStart ? null : steps[i].Time;
                }
                setTime = steps[i].Time == darkEnd ? null : steps[i].Time;
            }
        }

        return new VisibilityWindow
        {
            IsComputed = true,
            DarkWindowStart = darkStart,
            DarkWindowEnd = darkEnd,
            RiseTime = riseTime,
            SetTime = setTime,
            Duration = TimeSpan.FromMinutes(visibleMinutes),
            AverageAltitudeDegrees = visibleAltCount > 0 ? visibleAltSum / visibleAltCount : 0,
            PeakAltitudeDegrees = peakAlt,
            PeakAzimuthDegrees = peakAz,
            PeakClearanceDegrees = Math.Max(0, peakClearance),
            PeakTime = peakTime,
            MoonSeparationDegrees = moonSep,
            VisibilityFraction = visibleMinutes / totalDarkMinutes,
        };
    }

    /// <summary>
    /// Returns altitude samples for a fixed-coordinate or moving object over the night — used for altitude plots.
    /// </summary>
    public List<(DateTime Time, double Alt, double HorizAlt, double Az)> GetAltitudeSamples(
        Func<DateTime, (double Ra, double Dec)> getPosition,
        DateOnly observingDate,
        ObservationSite site,
        HorizonProfile horizon,
        int stepMinutes = 10)
    {
        var (darkStart, darkEnd) = AstronomyService.GetAstronomicalDarkness(observingDate, site);
        var samples = new List<(DateTime, double, double, double)>();
        var t = darkStart;

        while (t <= darkEnd)
        {
            var (ra, dec) = getPosition(t);
            var (alt, az) = AstronomyService.EquatorialToHorizontal(ra, dec, t,
                site.LatitudeDegrees, site.LongitudeDegrees);
            samples.Add((t, alt, horizon.GetAltitudeAt(az), az));
            t = t.AddMinutes(stepMinutes);
        }

        return samples;
    }
}
