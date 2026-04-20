using CommunityToolkit.Mvvm.ComponentModel;
using AstroPlanner.Models;
using AstroPlanner.Services;
using Avalonia.Media;

namespace AstroPlanner.ViewModels;

/// <summary>
/// Wraps a celestial object (DSO or solar system) plus its computed visibility
/// for display in the planner DataGrid.
/// </summary>
public partial class ObjectRowViewModel : ObservableObject
{
    public DeepSkyObject? DsoSource { get; }
    public SolarSystemObject? SolarSystemSource { get; }
    public CometObject? CometSource { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisStartDisplay))]
    [NotifyPropertyChangedFor(nameof(VisEndDisplay))]
    [NotifyPropertyChangedFor(nameof(VisStart12h))]
    [NotifyPropertyChangedFor(nameof(VisEnd12h))]
    [NotifyPropertyChangedFor(nameof(PeakTimeDisplay))]
    [NotifyPropertyChangedFor(nameof(ScoreDisplay))]
    private VisibilityWindow _visibility = VisibilityWindow.NotComputed;

    private int? _bortleClass;
    public int? BortleClass
    {
        get => _bortleClass;
        set
        {
            _bortleClass = value;
            OnPropertyChanged(nameof(Score));
            OnPropertyChanged(nameof(ScoreDisplay));
            OnPropertyChanged(nameof(SortScore));
            OnPropertyChanged(nameof(SkyFactor));
            OnPropertyChanged(nameof(SkyQualityLabel));
            OnPropertyChanged(nameof(SkyQualityColor));
        }
    }

    private bool _applySkyToScore;
    public bool ApplySkyToScore
    {
        get => _applySkyToScore;
        set
        {
            _applySkyToScore = value;
            OnPropertyChanged(nameof(Score));
            OnPropertyChanged(nameof(ScoreDisplay));
            OnPropertyChanged(nameof(SortScore));
        }
    }

    private TimeZoneInfo _timeZone = TimeZoneInfo.Local;
    public TimeZoneInfo TimeZone
    {
        get => _timeZone;
        set
        {
            _timeZone = value;
            OnPropertyChanged(nameof(VisStartDisplay));
            OnPropertyChanged(nameof(VisEndDisplay));
            OnPropertyChanged(nameof(VisStart12h));
            OnPropertyChanged(nameof(VisEnd12h));
            OnPropertyChanged(nameof(PeakTimeDisplay));
        }
    }

    public ObjectRowViewModel(DeepSkyObject dso)
    {
        DsoSource = dso;
    }

    public ObjectRowViewModel(SolarSystemObject planet)
    {
        SolarSystemSource = planet;
    }

    public ObjectRowViewModel(CometObject comet)
    {
        CometSource = comet;
    }

    // ── Display properties used by the DataGrid ──────────────────────────────

    public string PrimaryName => DsoSource?.DisplayName ?? SolarSystemSource?.Name ?? CometSource?.DisplayName ?? "";
    public string CatalogIds  => DsoSource?.CatalogIds  ?? SolarSystemSource?.BodyType.ToString()
                                 ?? CometSource?.Designation ?? "";

    public string TypeDisplay => DsoSource != null
        ? DsoSource.Type.ToDisplayString()
        : SolarSystemSource?.BodyType.ToString()
          ?? (CometSource != null ? "Comet" : "");

    public string Constellation => DsoSource != null
        ? ConstellationNames.Expand(DsoSource.Constellation)
        : "—";

    public double? Magnitude => DsoSource?.DisplayMagnitude is double m and < 90 ? m
        : SolarSystemSource?.Magnitude
          ?? CometSource?.Magnitude;

    public string MagnitudeDisplay => Magnitude is double mag ? mag.ToString("F1") : "—";

    public string SizeDisplay => DsoSource?.SizeDisplay
        ?? (SolarSystemSource?.AngularDiameterArcmin is double d ? $"{d:F1}'" : "—");

    // Visibility-derived display properties
    public string DurationDisplay
    {
        get
        {
            if (!Visibility.IsComputed) return "…";
            if (!Visibility.IsVisible) return "Not visible";
            var d = Visibility.Duration;
            return d.TotalHours >= 1
                ? $"{(int)d.TotalHours}h {d.Minutes:D2}m"
                : $"{d.Minutes}m";
        }
    }

    public string VisStartDisplay
    {
        get
        {
            if (!Visibility.IsComputed) return "…";
            if (!Visibility.IsVisible) return "—";
            var t = Visibility.RiseTime ?? Visibility.DarkWindowStart;
            return TimeZoneInfo.ConvertTimeFromUtc(t, TimeZone).ToString("HH:mm");
        }
    }

    public string VisEndDisplay
    {
        get
        {
            if (!Visibility.IsComputed) return "…";
            if (!Visibility.IsVisible) return "—";
            var t = Visibility.SetTime ?? Visibility.DarkWindowEnd;
            return TimeZoneInfo.ConvertTimeFromUtc(t, TimeZone).ToString("HH:mm");
        }
    }

    public string VisStart12h
    {
        get
        {
            if (!Visibility.IsComputed || !Visibility.IsVisible) return "";
            var t = Visibility.RiseTime ?? Visibility.DarkWindowStart;
            return TimeZoneInfo.ConvertTimeFromUtc(t, TimeZone).ToString("h:mm tt");
        }
    }

    public string VisEnd12h
    {
        get
        {
            if (!Visibility.IsComputed || !Visibility.IsVisible) return "";
            var t = Visibility.SetTime ?? Visibility.DarkWindowEnd;
            return TimeZoneInfo.ConvertTimeFromUtc(t, TimeZone).ToString("h:mm tt");
        }
    }

    /// <summary>"N" if the object peaks in the northern sky (az &lt; 90° or &gt; 270°), else "S".</summary>
    public string TransitDirectionDisplay
    {
        get
        {
            if (!Visibility.IsVisible) return "";
            double az = Visibility.PeakAzimuthDegrees;
            return az < 90 || az > 270 ? "N" : "S";
        }
    }

    public string PeakAltDisplay => Visibility.IsVisible
        ? $"{Visibility.PeakAltitudeDegrees:F0}° {TransitDirectionDisplay}" : "—";

    public string PeakClearanceDisplay => Visibility.IsVisible
        ? $"{Visibility.PeakClearanceDegrees:F0}°" : "—";

    public string PeakTimeDisplay => Visibility.IsVisible
        ? TimeZoneInfo.ConvertTimeFromUtc(Visibility.PeakTime, TimeZone).ToString("HH:mm") : "—";

    public string MoonSepDisplay => Visibility.IsVisible
        ? $"{Visibility.MoonSeparationDegrees:F0}°" : "—";

    // ── Observability score (0–100) ──────────────────────────────────────────

    public double Score
    {
        get
        {
            if (!Visibility.IsVisible) return 0;

            // 1. Visibility fraction (25pts): how much of the dark window it's above horizon
            double fracScore = Visibility.VisibilityFraction * 25;

            // 2. Average altitude (20pts): full score at ≥45° average
            double avgScore = Math.Min(Visibility.AverageAltitudeDegrees / 45.0, 1.0) * 20;

            // 3. Moon separation (20pts): full score at ≥90°
            double moonScore = Math.Min(Visibility.MoonSeparationDegrees / 90.0, 1.0) * 20;

            // 4. Brightness (15pts): prefer surface brightness for extended objects,
            //    fall back to integrated magnitude for stars/planets/clusters
            double brightScore;
            if (DsoSource?.SurfaceBrightness is double sb && sb > 0 && sb < 99)
            {
                // Surface brightness in mag/□″: ~10 (very bright) → ~25 (very faint)
                brightScore = Math.Clamp((25.0 - sb) / 15.0, 0, 1) * 15;
            }
            else if (Magnitude is double mag)
            {
                // Integrated magnitude: 0 → 15pts, 15 → 0pts
                brightScore = Math.Clamp((15.0 - mag) / 15.0, 0, 1) * 15;
            }
            else
            {
                brightScore = 7.5; // neutral when unknown
            }

            // 5. Size (10pts): log scale — 1′ → ~0pts, 5′ → ~5pts, ≥30′ → 10pts
            double sizeScore = 0;
            double? arcmin = DsoSource?.MajorAxisArcmin ?? SolarSystemSource?.AngularDiameterArcmin;
            if (arcmin is double s && s > 0)
                sizeScore = Math.Clamp(Math.Log10(Math.Max(s, 1)) / Math.Log10(30), 0, 1) * 10;

            // 6. Peak altitude (10pts): steep penalty below 20°
            double peak = Visibility.PeakAltitudeDegrees;
            double peakScore = peak < 10 ? 0
                : peak < 20 ? (peak - 10) / 10.0 * 3
                : Math.Min((peak - 20) / 70.0, 1.0) * 7 + 3;

            double raw = fracScore + avgScore + moonScore + brightScore + sizeScore + peakScore;
            return Math.Round(_applySkyToScore ? raw * SkyFactor : raw, 1);
        }
    }

    public string ScoreDisplay => Visibility.IsComputed
        ? (Visibility.IsVisible ? $"{Score:F0}" : "0")
        : "…";

    public double SortScore => Visibility.IsVisible ? Score : (Visibility.IsComputed ? 0 : -1);

    // ── Sky quality (Bortle) ─────────────────────────────────────────────────

    /// <summary>
    /// Detectability factor [0–1] based on Bortle class and object surface brightness.
    /// Returns 1.0 when Bortle class is unknown (no penalty applied).
    /// </summary>
    public double SkyFactor
    {
        get
        {
            if (_bortleClass == null || DsoSource == null) return 1.0;
            return SkyQuality.ComputeDetectabilityFactor(DsoSource, _bortleClass.Value);
        }
    }

    /// <summary>Short label: Excellent / Good / Fair / Poor / Marginal, or "—" when no Bortle data.</summary>
    public string SkyQualityLabel
    {
        get
        {
            if (_bortleClass == null || DsoSource == null) return "—";
            return SkyQuality.GetQualityLabel(SkyFactor);
        }
    }

    /// <summary>Color for the sky quality label.</summary>
    public IBrush SkyQualityColor
    {
        get
        {
            double f = SkyFactor;
            if (_bortleClass == null || DsoSource == null) return Brushes.Gray;
            return f >= 0.70 ? new SolidColorBrush(Color.FromRgb(80, 200, 100))
                 : f >= 0.45 ? new SolidColorBrush(Color.FromRgb(220, 180, 50))
                 : new SolidColorBrush(Color.FromRgb(220, 80, 60));
        }
    }

    /// <summary>Numeric sort key for sky quality (higher = better).</summary>
    public double SortSkyQuality => _bortleClass != null && DsoSource != null ? SkyFactor : 2.0;

    // ── Imaging setup ────────────────────────────────────────────────────────

    private IReadOnlyList<ImagingSetup> _setups = [];
    public IReadOnlyList<ImagingSetup> Setups
    {
        get => _setups;
        set
        {
            _setups = value;
            OnPropertyChanged(nameof(BestSetupDisplay));
            OnPropertyChanged(nameof(SortBestFill));
        }
    }

    public string BestSetupDisplay
    {
        get
        {
            if (_setups.Count == 0) return "—";
            if (DsoSource?.MajorAxisArcmin is not double maj || maj <= 0) return "—";
            var best = FovCalculator.BestSetup(_setups, maj);
            if (best == null) return "—";
            var (w, h) = FovCalculator.FovArcmin(best);
            return $"{best.Name}   {FovCalculator.FormatArcmin(w)}×{FovCalculator.FormatArcmin(h)}";
        }
    }

    public double SortBestFill
    {
        get
        {
            if (_setups.Count == 0 || DsoSource?.MajorAxisArcmin is not double maj || maj <= 0)
                return -1;
            var best = FovCalculator.BestSetup(_setups, maj);
            return best == null ? -1 : Math.Abs(FovCalculator.FillPercent(best, maj)
                - FovCalculator.TargetFillPct(maj));
        }
    }

    // Sort keys (numeric, for ViewModel sorting)
    public double SortDuration  => Visibility.Duration.TotalMinutes;
    public double SortMag       => Magnitude ?? 99;
    public double SortPeakAlt   => Visibility.IsVisible ? Visibility.PeakAltitudeDegrees : -999;
    public double SortClearance => Visibility.IsVisible ? Visibility.PeakClearanceDegrees : -999;
    public double SortMoonSep   => Visibility.IsVisible ? Visibility.MoonSeparationDegrees : -999;
}
