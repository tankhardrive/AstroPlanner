using AstroPlanner.Models;

namespace AstroPlanner.Services;

public static class FovCalculator
{
    /// <summary>Plate scale in arcseconds per pixel.</summary>
    public static double PlateScaleArcsecPx(double focalLengthMm, double pixelSizeMicrons)
        => focalLengthMm > 0 ? 206.265 * pixelSizeMicrons / focalLengthMm : 0;

    /// <summary>FOV in arcminutes for a given setup.</summary>
    public static (double WidthArcmin, double HeightArcmin) FovArcmin(ImagingSetup s)
    {
        double ps = PlateScaleArcsecPx(s.FocalLengthMm, s.PixelSizeMicrons);
        return (ps * s.SensorWidthPixels / 60.0, ps * s.SensorHeightPixels / 60.0);
    }

    /// <summary>
    /// What fraction of the longer FOV axis the object's major axis fills, as a percentage.
    /// Returns 0 if the setup has no usable data.
    /// </summary>
    public static double FillPercent(ImagingSetup s, double objectMajorArcmin)
    {
        var (w, h) = FovArcmin(s);
        double longSide = Math.Max(w, h);
        return longSide > 0 ? objectMajorArcmin / longSide * 100 : 0;
    }

    /// <summary>
    /// Ideal fill ratio target for an object of a given angular size.
    /// Small objects: ~40% fill so they're well-framed with context.
    /// Large objects: ~85-90% fill so the whole thing is captured.
    /// </summary>
    public static double TargetFillPct(double majorArcmin)
    {
        if (majorArcmin < 5)   return 40;
        if (majorArcmin < 20)  return 50;
        if (majorArcmin < 60)  return 65;
        if (majorArcmin < 120) return 80;
        return 90;
    }

    /// <summary>
    /// Returns true when the setup has enough data to compute FOV.
    /// Aperture is optional — only focal length + camera data are required.
    /// </summary>
    public static bool IsUsable(ImagingSetup s)
        => s.FocalLengthMm > 0
        && s.PixelSizeMicrons > 0
        && s.SensorWidthPixels > 0
        && s.SensorHeightPixels > 0;

    /// <summary>
    /// Returns the setup whose fill ratio is closest to the ideal target for objectMajorArcmin.
    /// </summary>
    public static ImagingSetup? BestSetup(IReadOnlyList<ImagingSetup> setups, double objectMajorArcmin)
    {
        if (setups.Count == 0 || objectMajorArcmin <= 0) return null;
        double target = TargetFillPct(objectMajorArcmin);
        return setups
            .Where(IsUsable)
            .OrderBy(s => Math.Abs(FillPercent(s, objectMajorArcmin) - target))
            .FirstOrDefault();
    }

    /// <summary>Format arcminutes: switch to degrees above 60′.</summary>
    public static string FormatArcmin(double arcmin)
        => arcmin >= 60 ? $"{arcmin / 60.0:F1}°" : $"{arcmin:F0}′";
}
