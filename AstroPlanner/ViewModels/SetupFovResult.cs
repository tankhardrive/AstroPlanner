using AstroPlanner.Services;

namespace AstroPlanner.ViewModels;

/// <summary>
/// Per-setup FOV and fit information for a specific target. Used in the detail panel.
/// </summary>
public class SetupFovResult
{
    public string SetupName      { get; init; } = "";
    public string TelescopeName  { get; init; } = "";
    public string CameraName     { get; init; } = "";
    public double FovWidthArcmin  { get; init; }
    public double FovHeightArcmin { get; init; }
    public double PlateScaleArcsecPx { get; init; }

    /// <summary>Null when the object has no angular size on record.</summary>
    public double? FillPercent { get; init; }

    /// <summary>True when the object's major axis fits within the longer FOV dimension.</summary>
    public bool ObjectFits { get; init; }

    /// <summary>Highlighted as the best-matched setup for this target.</summary>
    public bool IsBestMatch { get; init; }

    // ── Display properties for AXAML binding ────────────────────────────────

    public string FovDisplay
        => $"{FovCalculator.FormatArcmin(FovWidthArcmin)} × {FovCalculator.FormatArcmin(FovHeightArcmin)}";

    public string PlateScaleDisplay => $"{PlateScaleArcsecPx:F2}″/px";

    public string FillDisplay => FillPercent.HasValue ? $"{FillPercent.Value:F0}%" : "—";

    public string FitDisplay => FillPercent.HasValue
        ? (ObjectFits ? "Fits" : "Too large")
        : "";

    /// <summary>Combined scope/camera label, omitting blanks.</summary>
    public string GearLabel
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(TelescopeName) && !string.IsNullOrWhiteSpace(CameraName))
                return $"{TelescopeName}  ·  {CameraName}";
            if (!string.IsNullOrWhiteSpace(TelescopeName)) return TelescopeName;
            if (!string.IsNullOrWhiteSpace(CameraName)) return CameraName;
            return "";
        }
    }
}
