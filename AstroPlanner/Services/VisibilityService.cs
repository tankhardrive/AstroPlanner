using AstroPlanner.Models;

namespace AstroPlanner.Services;

/// <summary>
/// Computes horizon-aware visibility windows for celestial objects over a single night.
/// </summary>
public class VisibilityService
{
    private readonly AstronomyService _astronomy = new();

    /// <summary>
    /// Compute visibility for a fixed-coordinate deep sky object.
    /// </summary>
    public VisibilityWindow ComputeDso(
        DeepSkyObject obj,
        DateOnly observingDate,
        ObservationSite site,
        HorizonProfile horizon,
        int stepMinutes,
        (double RaDeg, double DecDeg, double IllumPct) moonInfo)
    {
        return Compute(
            obj.RaDegrees, obj.DecDegrees,
            isFixedCoord: true,
            bodyType: null,
            observingDate, site, horizon, stepMinutes, moonInfo);
    }

    /// <summary>
    /// Compute visibility for a solar system object (position recomputed each step).
    /// </summary>
    public VisibilityWindow ComputeSolarSystem(
        SolarSystemObject obj,
        DateOnly observingDate,
        ObservationSite site,
        HorizonProfile horizon,
        int stepMinutes,
        (double RaDeg, double DecDeg, double IllumPct) moonInfo)
    {
        return Compute(
            0, 0,
            isFixedCoord: false,
            bodyType: obj.BodyType,
            observingDate, site, horizon, stepMinutes, moonInfo);
    }

    private VisibilityWindow Compute(
        double fixedRa, double fixedDec,
        bool isFixedCoord,
        SolarSystemBodyType? bodyType,
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
        double peakRa = fixedRa, peakDec = fixedDec;
        double visibleAltSum = 0;
        int visibleAltCount = 0;

        // Sample the night
        var steps = new List<(DateTime Time, double Alt, double HorizAlt)>();
        var t = darkStart;

        while (t <= darkEnd)
        {
            double ra, dec;
            if (isFixedCoord)
            {
                ra = fixedRa;
                dec = fixedDec;
            }
            else
            {
                (ra, dec) = AstronomyService.GetPlanetPosition(bodyType!.Value, t);
            }

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

        // Moon separation at peak
        double moonSep = AstronomyService.AngularSeparationDeg(
            peakRa, peakDec, moonInfo.RaDeg, moonInfo.DecDeg);

        // Approximate rise/set times: first and last visible steps.
        // riseDetected guards against the null-as-sentinel ambiguity: riseTime can legitimately
        // be null (= "visible from start of darkness"), so we can't use null to mean "not yet found".
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
    /// Returns all (time, alt, horizAlt, az) samples for an object over the night — used for altitude plots.
    /// </summary>
    public List<(DateTime Time, double Alt, double HorizAlt, double Az)> GetAltitudeSamples(
        double raDeg, double decDeg,
        bool isFixedCoord,
        SolarSystemBodyType? bodyType,
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
            double ra, dec;
            if (isFixedCoord) { ra = raDeg; dec = decDeg; }
            else (ra, dec) = AstronomyService.GetPlanetPosition(bodyType!.Value, t);

            var (alt, az) = AstronomyService.EquatorialToHorizontal(ra, dec, t,
                site.LatitudeDegrees, site.LongitudeDegrees);
            samples.Add((t, alt, horizon.GetAltitudeAt(az), az));
            t = t.AddMinutes(stepMinutes);
        }

        return samples;
    }
}
