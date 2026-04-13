namespace AstroPlanner.Models;

/// <summary>
/// A telescope + camera pairing. FOV and plate scale are derived from these fields.
/// </summary>
public class ImagingSetup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Setup";

    // ── Telescope ────────────────────────────────────────────────────────────
    public string TelescopeName { get; set; } = "";

    /// <summary>Clear aperture in millimetres.</summary>
    public double ApertureMm { get; set; }

    /// <summary>Effective focal length in millimetres.</summary>
    public double FocalLengthMm { get; set; }

    // ── Camera ───────────────────────────────────────────────────────────────
    public string CameraName { get; set; } = "";

    /// <summary>Physical pixel pitch in microns (µm).</summary>
    public double PixelSizeMicrons { get; set; }

    public int SensorWidthPixels { get; set; }
    public int SensorHeightPixels { get; set; }
}
