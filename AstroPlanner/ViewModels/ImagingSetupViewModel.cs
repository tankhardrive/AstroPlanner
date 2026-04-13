using CommunityToolkit.Mvvm.ComponentModel;
using AstroPlanner.Models;
using AstroPlanner.Services;

namespace AstroPlanner.ViewModels;

/// <summary>
/// Editable wrapper around <see cref="ImagingSetup"/> for the Settings UI.
/// All inputs are strings (TextBox-friendly); derived values update live.
/// </summary>
public partial class ImagingSetupViewModel : ObservableObject
{
    public ImagingSetup Source { get; }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _telescopeName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FRatioDisplay))]
    private string _apertureMm = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FRatioDisplay))]
    [NotifyPropertyChangedFor(nameof(PlateScaleDisplay))]
    [NotifyPropertyChangedFor(nameof(FovDisplay))]
    private string _focalLengthMm = "";

    [ObservableProperty] private string _cameraName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlateScaleDisplay))]
    [NotifyPropertyChangedFor(nameof(FovDisplay))]
    private string _pixelSizeMicrons = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FovDisplay))]
    private string _sensorWidthPixels = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FovDisplay))]
    private string _sensorHeightPixels = "";

    // ── Derived display ──────────────────────────────────────────────────────

    public string FRatioDisplay
    {
        get
        {
            if (!double.TryParse(FocalLengthMm, Inv, out double fl) || fl <= 0) return "—";
            if (!double.TryParse(ApertureMm, Inv, out double ap) || ap <= 0) return "—";
            return $"f/{fl / ap:F1}";
        }
    }

    public string PlateScaleDisplay
    {
        get
        {
            if (!double.TryParse(FocalLengthMm, Inv, out double fl) || fl <= 0) return "—";
            if (!double.TryParse(PixelSizeMicrons, Inv, out double px) || px <= 0) return "—";
            return $"{FovCalculator.PlateScaleArcsecPx(fl, px):F2}″/px";
        }
    }

    public string FovDisplay
    {
        get
        {
            if (!double.TryParse(FocalLengthMm, Inv, out double fl) || fl <= 0) return "—";
            if (!double.TryParse(PixelSizeMicrons, Inv, out double px) || px <= 0) return "—";
            if (!int.TryParse(SensorWidthPixels, out int sw) || sw <= 0) return "—";
            if (!int.TryParse(SensorHeightPixels, out int sh) || sh <= 0) return "—";

            double ps = FovCalculator.PlateScaleArcsecPx(fl, px);
            double w = ps * sw / 60.0;
            double h = ps * sh / 60.0;
            return $"{FovCalculator.FormatArcmin(w)} × {FovCalculator.FormatArcmin(h)}";
        }
    }

    private static readonly System.Globalization.NumberStyles Ns =
        System.Globalization.NumberStyles.Float;
    private static readonly System.Globalization.CultureInfo Inv =
        System.Globalization.CultureInfo.InvariantCulture;

    public ImagingSetupViewModel(ImagingSetup source)
    {
        Source = source;
        LoadFromSource();
    }

    public void LoadFromSource()
    {
        Name              = Source.Name;
        TelescopeName     = Source.TelescopeName;
        ApertureMm        = Source.ApertureMm > 0 ? Source.ApertureMm.ToString("G", Inv) : "";
        FocalLengthMm     = Source.FocalLengthMm > 0 ? Source.FocalLengthMm.ToString("G", Inv) : "";
        CameraName        = Source.CameraName;
        PixelSizeMicrons  = Source.PixelSizeMicrons > 0 ? Source.PixelSizeMicrons.ToString("G", Inv) : "";
        SensorWidthPixels = Source.SensorWidthPixels > 0 ? Source.SensorWidthPixels.ToString() : "";
        SensorHeightPixels= Source.SensorHeightPixels > 0 ? Source.SensorHeightPixels.ToString() : "";
    }

    /// <summary>
    /// Parses all fields and writes them back to <see cref="Source"/>.
    /// Returns a validation error message, or null on success.
    /// </summary>
    public string? TrySave()
    {
        string n = string.IsNullOrWhiteSpace(Name) ? "New Setup" : Name.Trim();

        if (!double.TryParse(FocalLengthMm, Ns, Inv, out double fl) || fl <= 0)
            return "Focal length must be a positive number (mm).";
        if (!double.TryParse(PixelSizeMicrons, Ns, Inv, out double px) || px <= 0)
            return "Pixel size must be a positive number (µm).";
        if (!int.TryParse(SensorWidthPixels, out int sw) || sw <= 0)
            return "Sensor width must be a positive integer (pixels).";
        if (!int.TryParse(SensorHeightPixels, out int sh) || sh <= 0)
            return "Sensor height must be a positive integer (pixels).";

        double.TryParse(ApertureMm, Ns, Inv, out double ap); // optional

        Source.Name               = n;
        Source.TelescopeName      = TelescopeName.Trim();
        Source.ApertureMm         = ap;
        Source.FocalLengthMm      = fl;
        Source.CameraName         = CameraName.Trim();
        Source.PixelSizeMicrons   = px;
        Source.SensorWidthPixels  = sw;
        Source.SensorHeightPixels = sh;

        Name = n;
        return null;
    }
}
