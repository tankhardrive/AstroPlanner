namespace AstroPlanner.Models;

public enum SolarSystemBodyType
{
    Sun,
    Moon,
    Mercury,
    Venus,
    Mars,
    Jupiter,
    Saturn,
    Uranus,
    Neptune,
}

/// <summary>
/// Represents a solar system body. Position is recomputed per observation date.
/// Unlike DSOs, RA/Dec change significantly night to night.
/// </summary>
public class SolarSystemObject
{
    public string Name { get; init; } = "";
    public SolarSystemBodyType BodyType { get; init; }

    // Positions computed at runtime for the target date
    public double RaDegrees { get; set; }
    public double DecDegrees { get; set; }
    public double? Magnitude { get; set; }
    public double? AngularDiameterArcmin { get; set; }

    // Moon-specific
    public double? IlluminationPercent { get; set; }
    public double? PhaseAngleDegrees { get; set; }

    public static IReadOnlyList<SolarSystemObject> CreateDefaults() =>
    [
        new() { Name = "Moon",    BodyType = SolarSystemBodyType.Moon },
        new() { Name = "Mercury", BodyType = SolarSystemBodyType.Mercury },
        new() { Name = "Venus",   BodyType = SolarSystemBodyType.Venus },
        new() { Name = "Mars",    BodyType = SolarSystemBodyType.Mars },
        new() { Name = "Jupiter", BodyType = SolarSystemBodyType.Jupiter },
        new() { Name = "Saturn",  BodyType = SolarSystemBodyType.Saturn },
        new() { Name = "Uranus",  BodyType = SolarSystemBodyType.Uranus },
        new() { Name = "Neptune", BodyType = SolarSystemBodyType.Neptune },
    ];
}
