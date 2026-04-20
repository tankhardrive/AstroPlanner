namespace AstroPlanner.Models;

/// <summary>
/// Static helpers for sky quality (Bortle scale) and per-object detectability factors.
/// </summary>
public static class SkyQuality
{
    // Sky background brightness (mag/arcsec²) per Bortle class (index 1–9)
    private static readonly double[] SkyBrightnessMag =
    [
        0,     // index 0 unused
        22.0,  // B1 — Excellent dark-sky site
        21.5,  // B2
        21.0,  // B3
        20.4,  // B4
        19.5,  // B5
        18.5,  // B6
        17.5,  // B7
        16.5,  // B8
        15.5,  // B9 — Inner-city sky
    ];

    // Fractional sensitivity to light pollution by type.
    // Higher = more strongly degraded by a brighter sky.
    private static readonly Dictionary<ObjectType, double> TypeSensitivity = new()
    {
        { ObjectType.Galaxy,             0.80 },
        { ObjectType.GalaxyPair,         0.80 },
        { ObjectType.GalaxyTriplet,      0.80 },
        { ObjectType.GalaxyGroup,        0.80 },
        { ObjectType.OpenCluster,        0.05 },
        { ObjectType.GlobularCluster,    0.25 },
        { ObjectType.ClusterNebula,      0.50 },
        { ObjectType.PlanetaryNebula,    0.15 },
        { ObjectType.EmissionNebula,     0.65 },
        { ObjectType.ReflectionNebula,   0.65 },
        { ObjectType.SupernovaRemnant,   0.85 },
        { ObjectType.HiiRegion,          0.85 },
        { ObjectType.DarkNebula,         0.85 },
        { ObjectType.BrightNebula,       0.50 },
        { ObjectType.Nebula,             0.65 },
        { ObjectType.StellarAssociation, 0.40 },
        { ObjectType.Star,               0.02 },
        { ObjectType.DoubleStar,         0.02 },
    };

    /// <summary>
    /// Computes a detectability factor [0–1] for a DSO under a sky of the given Bortle class.
    /// Uses surface-brightness contrast when available; falls back to type-sensitivity tiers.
    /// </summary>
    public static double ComputeDetectabilityFactor(DeepSkyObject dso, int bortle)
    {
        int b = Math.Clamp(bortle, 1, 9);
        double skyBrightness = SkyBrightnessMag[b];

        // Primary path: surface brightness contrast
        if (dso.SurfaceBrightness is double objectSB && objectSB > 0 && objectSB < 99)
        {
            // contrast = how many times brighter the object's surface is vs the sky background.
            // Use log scale so the full range maps smoothly to [0,1]:
            //   contrast = 10   (object 2.5 mag/□″ brighter than sky) → 1.0  Excellent
            //   contrast =  1   (object matches sky brightness)        → 0.78 Good
            //   contrast = 0.1  (sky 2.5× brighter per □″)            → 0.55 Fair
            //   contrast = 0.01 (sky 25× brighter per □″)             → 0.33 Poor
            //   contrast ≈0.003 (sky ~80× brighter per □″)            → 0.20 Poor/Marginal
            double contrast    = Math.Pow(10, (skyBrightness - objectSB) / 2.5);
            double logContrast = Math.Log10(Math.Max(contrast, 0.0001));
            double sbFactor    = Math.Clamp((logContrast + 3.47) / 4.47, 0, 1);

            // Catalog mean SB for large galaxies is averaged across the whole ellipse,
            // including faint outer regions — it badly underestimates bright inner regions.
            // A floor based on integrated magnitude ensures bright objects (M31, M101, etc.)
            // are never labelled worse than their total flux warrants for imaging.
            double? mag     = dso.MagnitudeV ?? dso.MagnitudeB;
            double magFloor = mag is double m && m < 13.0
                ? Math.Clamp((13.0 - m) / 13.0, 0, 1) * 0.60
                : 0.0;

            return Math.Max(sbFactor, magFloor);
        }

        // Fallback: type-based sensitivity
        double sensitivity   = TypeSensitivity.TryGetValue(dso.Type, out var s) ? s : 0.50;
        double bortlePenalty = (b - 1) / 8.0;   // 0 at B1, 1 at B9
        return Math.Clamp(1.0 - sensitivity * bortlePenalty, 0, 1);
    }

    /// <summary>Short quality label for a detectability factor.</summary>
    public static string GetQualityLabel(double factor) => factor switch
    {
        >= 0.90 => "Excellent",
        >= 0.70 => "Good",
        >= 0.45 => "Fair",
        >= 0.20 => "Poor",
        _       => "Marginal",
    };

    /// <summary>Descriptive label for a Bortle class value.</summary>
    public static string GetBortleLabel(int bortle) => bortle switch
    {
        1 => "B1 – Excellent dark-sky site",
        2 => "B2 – Typical truly dark site",
        3 => "B3 – Rural sky",
        4 => "B4 – Rural/suburban transition",
        5 => "B5 – Suburban sky",
        6 => "B6 – Bright suburban sky",
        7 => "B7 – Suburban/urban transition",
        8 => "B8 – City sky",
        9 => "B9 – Inner-city sky",
        _ => $"B{bortle}",
    };
}
