using CommunityToolkit.Mvvm.ComponentModel;
using AstroPlanner.Models;

namespace AstroPlanner.ViewModels;

/// <summary>
/// Wraps a celestial object (DSO or solar system) plus its computed visibility
/// for display in the planner DataGrid.
/// </summary>
public partial class ObjectRowViewModel : ObservableObject
{
    public DeepSkyObject? DsoSource { get; }
    public SolarSystemObject? SolarSystemSource { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisStartDisplay))]
    [NotifyPropertyChangedFor(nameof(VisEndDisplay))]
    [NotifyPropertyChangedFor(nameof(VisStart12h))]
    [NotifyPropertyChangedFor(nameof(VisEnd12h))]
    [NotifyPropertyChangedFor(nameof(PeakTimeDisplay))]
    private VisibilityWindow _visibility = VisibilityWindow.NotComputed;

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

    // ── Display properties used by the DataGrid ──────────────────────────────

    public string PrimaryName => DsoSource?.DisplayName ?? SolarSystemSource?.Name ?? "";
    public string CatalogIds  => DsoSource?.CatalogIds  ?? SolarSystemSource?.BodyType.ToString() ?? "";

    public string TypeDisplay => DsoSource != null
        ? DsoSource.Type.ToDisplayString()
        : SolarSystemSource?.BodyType.ToString() ?? "";

    public string Constellation => DsoSource != null
        ? ConstellationNames.Expand(DsoSource.Constellation)
        : "—";

    public double? Magnitude => DsoSource?.DisplayMagnitude is double m and < 90 ? m
        : SolarSystemSource?.Magnitude;

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

    public string PeakAltDisplay => Visibility.IsVisible
        ? $"{Visibility.PeakAltitudeDegrees:F0}°" : "—";

    public string PeakClearanceDisplay => Visibility.IsVisible
        ? $"{Visibility.PeakClearanceDegrees:F0}°" : "—";

    public string PeakTimeDisplay => Visibility.IsVisible
        ? TimeZoneInfo.ConvertTimeFromUtc(Visibility.PeakTime, TimeZone).ToString("HH:mm") : "—";

    public string MoonSepDisplay => Visibility.IsVisible
        ? $"{Visibility.MoonSeparationDegrees:F0}°" : "—";

    // Sort keys (numeric, for ViewModel sorting)
    public double SortDuration  => Visibility.Duration.TotalMinutes;
    public double SortMag       => Magnitude ?? 99;
    public double SortPeakAlt   => Visibility.IsVisible ? Visibility.PeakAltitudeDegrees : -999;
    public double SortClearance => Visibility.IsVisible ? Visibility.PeakClearanceDegrees : -999;
    public double SortMoonSep   => Visibility.IsVisible ? Visibility.MoonSeparationDegrees : -999;
}
