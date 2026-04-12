namespace AstroPlanner.Models;

/// <summary>
/// A named observing location that bundles site coordinates, timezone, and horizon profile together.
/// Replaces the separate ObservationSite + HorizonProfiles model.
/// </summary>
public class ObservationLocation
{
    public string Name { get; set; } = "My Location";
    public double LatitudeDegrees { get; set; } = 0;
    public double LongitudeDegrees { get; set; } = 0;
    public double ElevationMeters { get; set; } = 0;

    /// <summary>IANA timezone ID, e.g. "America/New_York".</summary>
    public string TimeZoneId { get; set; } = "UTC";

    public HorizonProfile Horizon { get; set; } = HorizonProfile.Flat();

    /// <summary>Project this location into the ObservationSite shape that the astronomy services expect.</summary>
    public ObservationSite ToSite() => new()
    {
        Name = Name,
        LatitudeDegrees = LatitudeDegrees,
        LongitudeDegrees = LongitudeDegrees,
        ElevationMeters = ElevationMeters,
        TimeZoneId = TimeZoneId,
    };

    public TimeZoneInfo GetTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); }
        catch { return TimeZoneInfo.Local; }
    }
}
