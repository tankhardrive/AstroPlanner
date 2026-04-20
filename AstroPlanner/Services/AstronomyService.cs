using AASharp;
using AstroPlanner.Models;

namespace AstroPlanner.Services;

/// <summary>
/// Core astronomical calculations: coordinate transforms, twilight, moon, planets.
/// All input/output in degrees unless noted. DateTimes are UTC unless noted.
/// </summary>
public class AstronomyService
{
    // ── Julian Day ────────────────────────────────────────────────────────────

    public static DateTime FromJulianDay(double jd) =>
        new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddDays(jd - 2451545.0);

    public static double ToJulianDay(DateTime utc)
    {
        double dayFraction = utc.Day + utc.TimeOfDay.TotalDays;
        return AASDate.DateToJD(utc.Year, utc.Month, dayFraction, true);
    }

    // ── Coordinate transform ──────────────────────────────────────────────────

    /// <summary>
    /// Convert equatorial (RA/Dec, J2000 degrees) to horizontal (Alt/Az degrees)
    /// for a given UTC time and observer location.
    /// Returns Az in [0,360) measured from North through East.
    /// </summary>
    public static (double Alt, double Az) EquatorialToHorizontal(
        double raDeg, double decDeg,
        DateTime utc,
        double latDeg, double lonDeg)
    {
        double jd = ToJulianDay(utc);
        // Greenwich Apparent Sidereal Time in hours
        double gast = AASSidereal.ApparentGreenwichSiderealTime(jd);
        // Local Sidereal Time in hours (east longitude positive)
        double lst = gast + lonDeg / 15.0;
        lst = ((lst % 24) + 24) % 24;
        // Hour angle in HOURS (AASharp Equatorial2Horizontal requires decimal hours, not degrees)
        double haHours = lst - raDeg / 15.0;
        haHours = ((haHours % 24) + 24) % 24;
        if (haHours > 12) haHours -= 24; // [-12, 12]

        var horiz = AASCoordinateTransformation.Equatorial2Horizontal(haHours, decDeg, latDeg);
        double alt = horiz.Y;
        double az = (horiz.X + 180.0) % 360.0; // Meeus south-zero → north-zero
        if (az < 0) az += 360;
        return (alt, az);
    }

    // ── Sun ───────────────────────────────────────────────────────────────────

    public static (double RaDeg, double DecDeg) GetSunPosition(DateTime utc)
    {
        double jd = ToJulianDay(utc);
        double lon = AASSun.ApparentEclipticLongitude(jd, false);
        double lat = AASSun.ApparentEclipticLatitude(jd, false);
        double eps = AASNutation.TrueObliquityOfEcliptic(jd);
        var eq = AASCoordinateTransformation.Ecliptic2Equatorial(lon, lat, eps);
        return (eq.X * 15.0, eq.Y); // X is in hours → degrees
    }

    public static double GetSunAltitude(DateTime utc, double latDeg, double lonDeg)
    {
        var (ra, dec) = GetSunPosition(utc);
        var (alt, _) = EquatorialToHorizontal(ra, dec, utc, latDeg, lonDeg);
        return alt;
    }

    /// <summary>
    /// Find the start and end of astronomical darkness (Sun < -18°) for a given local date.
    /// Returns UTC times.
    /// </summary>
    public static (DateTime DarkStart, DateTime DarkEnd) GetAstronomicalDarkness(
        DateOnly localDate, ObservationSite site)
    {
        var tz = site.GetTimeZone();
        var lat = site.LatitudeDegrees;
        var lon = site.LongitudeDegrees;

        // Evening: search noon → 3 AM next day. At noon sun is well above -18°; at 3 AM well below.
        var localNoon = new DateTime(localDate.Year, localDate.Month, localDate.Day, 12, 0, 0);
        var evening_hi = localNoon.AddHours(15); // 3 AM next day

        var darkStart = BinarySearchTwilight(
            TimeZoneInfo.ConvertTimeToUtc(localNoon, tz),
            TimeZoneInfo.ConvertTimeToUtc(evening_hi, tz),
            lat, lon, crossingDown: true);

        // Morning: search 1 AM → noon next day.
        // Lo MUST be after full darkness (sun well below -18°). Using 1 AM ensures we're safely past
        // evening astronomical twilight for any location/season. Using 9 PM as lo breaks the binary
        // search because the sun isn't yet below -18° at that hour in many cases.
        var morning_lo = localNoon.AddHours(13); // 1 AM next day
        var morning_hi = localNoon.AddHours(24); // noon next day

        var darkEnd = BinarySearchTwilight(
            TimeZoneInfo.ConvertTimeToUtc(morning_lo, tz),
            TimeZoneInfo.ConvertTimeToUtc(morning_hi, tz),
            lat, lon, crossingDown: false);

        return (darkStart, darkEnd);
    }

    private static DateTime BinarySearchTwilight(
        DateTime lo, DateTime hi,
        double lat, double lon,
        bool crossingDown) // true = looking for sun crossing -18 going down, false = coming up
    {
        const double threshold = -18.0;

        for (int i = 0; i < 40; i++) // ~40 iterations → <1 sec precision
        {
            var mid = lo + (hi - lo) / 2;
            double alt = GetSunAltitude(mid, lat, lon);

            if (crossingDown)
            {
                if (alt > threshold) lo = mid;
                else hi = mid;
            }
            else
            {
                if (alt < threshold) lo = mid;
                else hi = mid;
            }
        }
        return lo + (hi - lo) / 2;
    }

    // ── Moon ─────────────────────────────────────────────────────────────────

    public static (double RaDeg, double DecDeg, double IlluminationPct) GetMoonPosition(DateTime utc)
    {
        double jd = ToJulianDay(utc);
        double lon = AASMoon.EclipticLongitude(jd);
        double lat = AASMoon.EclipticLatitude(jd);
        double eps = AASNutation.TrueObliquityOfEcliptic(jd);
        var eq = AASCoordinateTransformation.Ecliptic2Equatorial(lon, lat, eps);
        double ra = eq.X * 15.0;
        double dec = eq.Y;

        // Illumination from elongation
        var (sunRa, sunDec) = GetSunPosition(utc);
        double illum = ComputeIllumination(ra, dec, sunRa, sunDec);
        return (ra, dec, illum * 100.0);
    }

    private static double ComputeIllumination(double moonRa, double moonDec, double sunRa, double sunDec)
    {
        double d2r = Math.PI / 180.0;
        double cosSep =
            Math.Sin(moonDec * d2r) * Math.Sin(sunDec * d2r) +
            Math.Cos(moonDec * d2r) * Math.Cos(sunDec * d2r) * Math.Cos((moonRa - sunRa) * d2r);
        cosSep = Math.Clamp(cosSep, -1, 1);
        double elongation = Math.Acos(cosSep);
        return (1.0 - Math.Cos(elongation)) / 2.0;
    }

    // ── Planets ───────────────────────────────────────────────────────────────

    public static (double RaDeg, double DecDeg) GetPlanetPosition(SolarSystemBodyType body, DateTime utc)
    {
        double jd = ToJulianDay(utc);
        double eps = AASNutation.TrueObliquityOfEcliptic(jd);

        double lon, lat;
        switch (body)
        {
            case SolarSystemBodyType.Mercury:
                lon = AASMercury.EclipticLongitude(jd, false);
                lat = AASMercury.EclipticLatitude(jd, false);
                break;
            case SolarSystemBodyType.Venus:
                lon = AASVenus.EclipticLongitude(jd, false);
                lat = AASVenus.EclipticLatitude(jd, false);
                break;
            case SolarSystemBodyType.Mars:
                lon = AASMars.EclipticLongitude(jd, false);
                lat = AASMars.EclipticLatitude(jd, false);
                break;
            case SolarSystemBodyType.Jupiter:
                lon = AASJupiter.EclipticLongitude(jd, false);
                lat = AASJupiter.EclipticLatitude(jd, false);
                break;
            case SolarSystemBodyType.Saturn:
                lon = AASSaturn.EclipticLongitude(jd, false);
                lat = AASSaturn.EclipticLatitude(jd, false);
                break;
            case SolarSystemBodyType.Uranus:
                lon = AASUranus.EclipticLongitude(jd, false);
                lat = AASUranus.EclipticLatitude(jd, false);
                break;
            case SolarSystemBodyType.Neptune:
                lon = AASNeptune.EclipticLongitude(jd, false);
                lat = AASNeptune.EclipticLatitude(jd, false);
                break;
            default:
                return GetSunPosition(utc);
        }

        var eq = AASCoordinateTransformation.Ecliptic2Equatorial(lon, lat, eps);
        return (eq.X * 15.0, eq.Y);
    }

    // ── Comets ────────────────────────────────────────────────────────────────

    public static (double RaDeg, double DecDeg) GetCometPosition(Models.CometObject comet, DateTime utc)
    {
        double jd = ToJulianDay(utc);
        var elements = BuildNearParabolicElements(comet);
        var details = AASNearParabolic.Calculate(jd, ref elements, false);
        return (details.AstrometricGeocentricRA * 15.0, details.AstrometricGeocentricDeclination);
    }

    public static double? GetCometMagnitude(Models.CometObject comet, DateTime utc)
    {
        if (comet.MagnitudeH == null) return null;
        double jd = ToJulianDay(utc);
        var elements = BuildNearParabolicElements(comet);
        try
        {
            var details = AASNearParabolic.Calculate(jd, ref elements, false);
            double delta = details.AstrometricGeocentricDistance;
            double v = 0, r = 0;
            AASNearParabolic.CalulateTrueAnnomalyAndRadius(jd, ref elements, ref v, ref r);
            if (delta <= 0 || r <= 0) return null;
            double G = comet.MagnitudeG ?? 4.0;
            return comet.MagnitudeH.Value + 5.0 * Math.Log10(delta) + 2.5 * G * Math.Log10(r);
        }
        catch { return null; }
    }

    private static AASNearParabolicObjectElements BuildNearParabolicElements(Models.CometObject comet) =>
        new()
        {
            q = comet.PerihelionDistanceAu,
            e = comet.Eccentricity,
            i = comet.InclinationDeg,
            w = comet.ArgPerihelionDeg,
            omega = comet.LongAscNodeDeg,
            JDEquinox = 2451545.0,
            T = comet.PerihelionJd,
        };

    // ── Angular separation ────────────────────────────────────────────────────

    public static double AngularSeparationDeg(double ra1, double dec1, double ra2, double dec2)
    {
        double d2r = Math.PI / 180.0;
        double cos =
            Math.Sin(dec1 * d2r) * Math.Sin(dec2 * d2r) +
            Math.Cos(dec1 * d2r) * Math.Cos(dec2 * d2r) * Math.Cos((ra1 - ra2) * d2r);
        return Math.Acos(Math.Clamp(cos, -1, 1)) / d2r;
    }
}
