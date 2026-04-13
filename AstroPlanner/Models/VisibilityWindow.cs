namespace AstroPlanner.Models;

/// <summary>
/// Describes when a celestial object is visible above the custom horizon
/// during a single night's astronomical darkness window.
/// </summary>
public class VisibilityWindow
{
    public static readonly VisibilityWindow NotComputed = new() { IsComputed = false };
    public static readonly VisibilityWindow NeverVisible = new() { IsComputed = true, Duration = TimeSpan.Zero };

    public bool IsComputed { get; init; }
    public bool IsVisible => IsComputed && Duration > TimeSpan.Zero;

    /// <summary>UTC start of astronomical darkness for this night.</summary>
    public DateTime DarkWindowStart { get; init; }

    /// <summary>UTC end of astronomical darkness for this night.</summary>
    public DateTime DarkWindowEnd { get; init; }

    /// <summary>When the object first clears the horizon. Null = already above at darkness start.</summary>
    public DateTime? RiseTime { get; init; }

    /// <summary>When the object drops below the horizon. Null = still above at darkness end.</summary>
    public DateTime? SetTime { get; init; }

    /// <summary>Total duration above the horizon within the darkness window.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Mean altitude across all visible steps tonight, in degrees.</summary>
    public double AverageAltitudeDegrees { get; init; }

    /// <summary>Highest altitude reached tonight, in degrees.</summary>
    public double PeakAltitudeDegrees { get; init; }

    /// <summary>Azimuth (north-zero, clockwise) at peak altitude.</summary>
    public double PeakAzimuthDegrees { get; init; }

    /// <summary>Peak altitude minus horizon altitude at that azimuth (clearance above horizon).</summary>
    public double PeakClearanceDegrees { get; init; }

    public DateTime PeakTime { get; init; }

    /// <summary>Angular separation from the Moon at peak time, in degrees.</summary>
    public double MoonSeparationDegrees { get; init; }

    /// <summary>Fraction of the astronomical darkness window during which the object is visible.</summary>
    public double VisibilityFraction { get; init; }
}
