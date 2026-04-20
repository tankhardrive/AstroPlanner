using System.Diagnostics;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AstroPlanner.Models;
using AstroPlanner.Services;
using System.Collections.ObjectModel;

namespace AstroPlanner.ViewModels;

public partial class ObjectDetailViewModel : ViewModelBase
{
    private readonly ImageService _imageService;
    private readonly VisibilityService _visService;

    [ObservableProperty] private ObjectRowViewModel? _source;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowNoImage))] private Bitmap? _image;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowNoImage))] private bool _imageLoading;
    [ObservableProperty] private IReadOnlyList<(DateTime Time, double Alt, double HorizAlt, double Az)>? _plotSamples;

    public bool ShowNoImage => Image == null && !ImageLoading;

    // Per-source image cache for the current object
    private readonly Dictionary<string, Bitmap?> _imagesBySource = new();
    private CancellationTokenSource? _imageCts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentSourceLabel))]
    [NotifyPropertyChangedFor(nameof(CurrentSourceCredit))]
    [NotifyPropertyChangedFor(nameof(CanGoPrev))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    private int _currentSourceIndex;

    public string CurrentSourceLabel  => ImageService.Sources[CurrentSourceIndex].Label;
    public string CurrentSourceCredit => ImageService.Sources[CurrentSourceIndex].Credit;
    public bool CanGoPrev => CurrentSourceIndex > 0;
    public bool CanGoNext => CurrentSourceIndex < ImageService.Sources.Count - 1;

    // Settings reference — set by MainWindowViewModel when opening detail
    public ObservationSite? Site { get; set; }
    public HorizonProfile? Horizon { get; set; }
    public DateOnly ObservingDate { get; set; }

    private IReadOnlyList<ImagingSetup> _setups = [];
    public IReadOnlyList<ImagingSetup> Setups
    {
        get => _setups;
        set { _setups = value; OnPropertyChanged(nameof(SetupResults)); }
    }

    public IReadOnlyList<SetupFovResult> SetupResults
    {
        get
        {
            if (_setups.Count == 0) return [];

            double? maj = Source?.DsoSource?.MajorAxisArcmin
                       ?? Source?.SolarSystemSource?.AngularDiameterArcmin;

            ImagingSetup? best = maj.HasValue ? FovCalculator.BestSetup(_setups, maj.Value) : null;

            return _setups
                .Where(FovCalculator.IsUsable)
                .Select(s =>
                {
                    var (w, h) = FovCalculator.FovArcmin(s);
                    double ps = FovCalculator.PlateScaleArcsecPx(s.FocalLengthMm, s.PixelSizeMicrons);
                    double? fill = maj.HasValue ? FovCalculator.FillPercent(s, maj.Value) : null;
                    bool fits = !maj.HasValue || maj.Value <= Math.Max(w, h);
                    return new SetupFovResult
                    {
                        SetupName           = s.Name,
                        TelescopeName       = s.TelescopeName,
                        CameraName          = s.CameraName,
                        FovWidthArcmin      = w,
                        FovHeightArcmin     = h,
                        PlateScaleArcsecPx  = ps,
                        FillPercent         = fill,
                        ObjectFits          = fits,
                        IsBestMatch         = s.Id == best?.Id,
                    };
                })
                .OrderBy(r => !r.IsBestMatch)
                .ToList();
        }
    }

    public ObjectDetailViewModel(ImageService imageService, VisibilityService visService)
    {
        _imageService = imageService;
        _visService = visService;
    }

    public bool IsVisible => Source != null;
    public TimeZoneInfo ObservingTimeZone => Source?.TimeZone ?? TimeZoneInfo.Local;

    // Detail display properties
    public string DetailName     => Source?.PrimaryName ?? "";
    public string DetailCatalogs => Source?.CatalogIds ?? "";
    public string DetailType     => Source?.DsoSource?.Type.ToDisplayString()
                                    ?? Source?.SolarSystemSource?.BodyType.ToString()
                                    ?? (Source?.CometSource != null ? "Comet" : "");
    public string DetailConst    => Source?.DsoSource is DeepSkyObject dso0
                                    ? ConstellationNames.Expand(dso0.Constellation) : "—";
    public string DetailMag      => Source?.MagnitudeDisplay ?? "—";
    public string DetailSize     => Source?.SizeDisplay ?? "—";
    public string DetailSurfBr   => Source?.DsoSource?.SurfaceBrightness is double sb
                                    ? $"{sb:F1} mag/□″" : "—";
    public string DetailHubble   => Source?.DsoSource?.HubbleType ?? "—";

    public string DetailRa  => Source?.DsoSource is DeepSkyObject dso1 ? FormatRa(dso1.RaDegrees)
                               : Source?.CometSource is CometObject c1 ? FormatRa(c1.RaDegrees) : "—";
    public string DetailDec => Source?.DsoSource is DeepSkyObject dso2 ? FormatDec(dso2.DecDegrees)
                               : Source?.CometSource is CometObject c2 ? FormatDec(c2.DecDegrees) : "—";

    public string DetailVisibility => Source?.Visibility is { IsVisible: true }
        ? $"{Source.VisStartDisplay} – {Source.VisEndDisplay}  ({Source.DurationDisplay})"
        : Source?.DurationDisplay ?? "—";

    public string DetailPeak    => Source?.Visibility is { IsVisible: true }
        ? $"{Source.PeakAltDisplay} at {Source.PeakTimeDisplay}" : "—";

    public string DetailMoonSep => Source?.MoonSepDisplay ?? "—";

    // ── AstroBin ──────────────────────────────────────────────────────────────

    private string AstroBinSearchTerm
    {
        get
        {
            if (Source?.DsoSource is DeepSkyObject dso)
            {
                if (dso.MessierNumber.HasValue) return $"M{dso.MessierNumber}";
                if (!string.IsNullOrEmpty(dso.CommonName)) return dso.CommonName;
                return dso.ShortName;
            }
            return Source?.PrimaryName ?? "";
        }
    }

    [RelayCommand]
    private void OpenAstroBin()
    {
        var term = Uri.EscapeDataString(AstroBinSearchTerm);
        var url = $"https://www.astrobin.com/search/?q={term}";
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch { /* best-effort */ }
    }

    // ── Aladin Lite ───────────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenAladin()
    {
        double ra, dec, fov;
        if (Source?.DsoSource is DeepSkyObject dso)
        {
            ra  = dso.RaDegrees;
            dec = dso.DecDegrees;
            double maj = dso.MajorAxisArcmin ?? 30;
            fov = Math.Clamp(maj / 60.0 * 3.0, 0.1, 5.0);
        }
        else if (Source?.SolarSystemSource is SolarSystemObject ss)
        {
            ra  = ss.RaDegrees;
            dec = ss.DecDegrees;
            fov = 0.5;
        }
        else if (Source?.CometSource is CometObject comet)
        {
            ra  = comet.RaDegrees;
            dec = comet.DecDegrees;
            fov = 1.0;
        }
        else return;

        var decStr = dec >= 0 ? $"+{dec:F5}" : $"{dec:F5}";
        var url = $"https://aladin.cds.unistra.fr/AladinLite/?target={ra:F5}+{decStr}&fov={fov:F2}&survey=P/DSS2/color";
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch { /* best-effort */ }
    }

    // ── Image navigation ──────────────────────────────────────────────────────

    [RelayCommand]
    private void PrevImage()
    {
        if (CurrentSourceIndex <= 0) return;
        CurrentSourceIndex--;
        ShowCachedOrLoadingState();
    }

    [RelayCommand]
    private void NextImage()
    {
        if (CurrentSourceIndex >= ImageService.Sources.Count - 1) return;
        CurrentSourceIndex++;
        ShowCachedOrLoadingState();
    }

    /// <summary>Immediately show a cached image for the current source, or show loading if still in flight.</summary>
    private void ShowCachedOrLoadingState()
    {
        var key = ImageService.Sources[CurrentSourceIndex].Key;
        if (_imagesBySource.TryGetValue(key, out var bmp))
        {
            ImageLoading = false;
            Image = bmp;   // already fetched (may be null if survey has no data)
        }
        else
        {
            ImageLoading = true;   // still fetching in background
            Image = null;
        }
    }

    // ── Source changed ────────────────────────────────────────────────────────

    partial void OnSourceChanged(ObjectRowViewModel? value)
    {
        // Ensure comet position and magnitude are available for display
        // even if the user hasn't pressed Calculate yet.
        if (value?.CometSource is CometObject comet)
        {
            var utc = new DateTime(ObservingDate.Year, ObservingDate.Month, ObservingDate.Day,
                                   4, 0, 0, DateTimeKind.Utc);
            if (comet.RaDegrees == 0 && comet.DecDegrees == 0)
            {
                var (ra, dec) = AstronomyService.GetCometPosition(comet, utc);
                comet.RaDegrees = ra;
                comet.DecDegrees = dec;
            }
            comet.Magnitude ??= AstronomyService.GetCometMagnitude(comet, utc);
        }

        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(DetailName));
        OnPropertyChanged(nameof(DetailCatalogs));
        OnPropertyChanged(nameof(DetailType));
        OnPropertyChanged(nameof(DetailConst));
        OnPropertyChanged(nameof(DetailMag));
        OnPropertyChanged(nameof(DetailSize));
        OnPropertyChanged(nameof(DetailSurfBr));
        OnPropertyChanged(nameof(DetailHubble));
        OnPropertyChanged(nameof(DetailRa));
        OnPropertyChanged(nameof(DetailDec));
        OnPropertyChanged(nameof(DetailVisibility));
        OnPropertyChanged(nameof(DetailPeak));
        OnPropertyChanged(nameof(DetailMoonSep));
        OnPropertyChanged(nameof(ObservingTimeZone));
        OnPropertyChanged(nameof(SetupResults));

        _imageCts?.Cancel();
        _imagesBySource.Clear();
        Image = null;
        PlotSamples = null;
        CurrentSourceIndex = 0;

        if (value == null) return;

        _ = PrefetchImagesAsync(value);
        _ = LoadPlotAsync(value);
    }

    /// <summary>
    /// Starts all source fetches concurrently.
    /// DSS (index 0) updates the displayed image and loading indicator as soon as it arrives.
    /// All other sources are cached silently in the background; if the user has already
    /// switched to that source the image is applied immediately when the fetch completes.
    /// </summary>
    private async Task PrefetchImagesAsync(ObjectRowViewModel row)
    {
        _imageCts?.Cancel();
        _imageCts = new CancellationTokenSource();
        var token = _imageCts.Token;

        double ra, dec, sizeDeg;
        if (row.DsoSource is DeepSkyObject dso)
        {
            ra = dso.RaDegrees;
            dec = dso.DecDegrees;
            double maj = dso.MajorAxisArcmin ?? 30;
            sizeDeg = Math.Clamp(maj / 60.0 * 2.5, 0.08, 3.0);
        }
        else if (row.SolarSystemSource is SolarSystemObject ss)
        {
            ra = ss.RaDegrees;
            dec = ss.DecDegrees;
            sizeDeg = 0.25;
        }
        else if (row.CometSource is CometObject comet)
        {
            ra = comet.RaDegrees;
            dec = comet.DecDegrees;
            sizeDeg = 0.5;
        }
        else return;

        // Kick off ALL source fetches concurrently right away
        var sources = ImageService.Sources;
        var tasks = sources.Select(s => _imageService.FetchAsync(ra, dec, sizeDeg, s.Key)).ToArray();

        // Primary source (DSS): show loading indicator, await, display
        ImageLoading = true;
        try
        {
            var primaryBytes = await tasks[0];
            if (token.IsCancellationRequested) return;
            _imagesBySource[sources[0].Key] = ToBitmap(primaryBytes, sources[0].Key);
            if (CurrentSourceIndex == 0) Image = _imagesBySource[sources[0].Key];
        }
        catch { _imagesBySource[sources[0].Key] = null; }
        finally
        {
            // Only clear the loading spinner if the user is still on source 0;
            // if they've already navigated away, ShowCachedOrLoadingState owns the state.
            if (!token.IsCancellationRequested && CurrentSourceIndex == 0)
                ImageLoading = false;
        }

        // Remaining sources: cache silently; if user is already on that tab, update it
        for (int i = 1; i < sources.Count; i++)
        {
            if (token.IsCancellationRequested) return;
            try
            {
                var bytes = await tasks[i];
                if (token.IsCancellationRequested) return;
                _imagesBySource[sources[i].Key] = ToBitmap(bytes, sources[i].Key);
                if (CurrentSourceIndex == i)
                {
                    ImageLoading = false;
                    Image = _imagesBySource[sources[i].Key];
                }
            }
            catch
            {
                _imagesBySource[sources[i].Key] = null;
                if (CurrentSourceIndex == i) ImageLoading = false;
            }
        }
    }

    private static Bitmap? ToBitmap(byte[]? bytes, string sourceKey = "")
    {
        if (bytes == null) return null;
        try
        {
            using var ms = new MemoryStream(bytes);
            return new Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }

    private Task LoadPlotAsync(ObjectRowViewModel row)
    {
        if (Site == null || Horizon == null) return Task.CompletedTask;

        return Task.Run(() =>
        {
            List<(DateTime Time, double Alt, double HorizAlt, double Az)>? samples;

            if (row.DsoSource is DeepSkyObject dso)
            {
                samples = _visService.GetAltitudeSamples(
                    _ => (dso.RaDegrees, dso.DecDegrees),
                    ObservingDate, Site, Horizon, stepMinutes: 5);
            }
            else if (row.SolarSystemSource is SolarSystemObject ss)
            {
                samples = _visService.GetAltitudeSamples(
                    t => AstronomyService.GetPlanetPosition(ss.BodyType, t),
                    ObservingDate, Site, Horizon, stepMinutes: 5);
            }
            else if (row.CometSource is CometObject comet)
            {
                samples = _visService.GetAltitudeSamples(
                    t => AstronomyService.GetCometPosition(comet, t),
                    ObservingDate, Site, Horizon, stepMinutes: 5);
            }
            else return;

            Avalonia.Threading.Dispatcher.UIThread.Post(() => PlotSamples = samples);
        });
    }

    private static string FormatRa(double degrees)
    {
        double hours = degrees / 15.0;
        int h = (int)hours;
        double mRem = (hours - h) * 60;
        int m = (int)mRem;
        double s = (mRem - m) * 60;
        return $"{h:D2}h {m:D2}m {s:F1}s";
    }

    private static string FormatDec(double degrees)
    {
        char sign = degrees >= 0 ? '+' : '-';
        double abs = Math.Abs(degrees);
        int d = (int)abs;
        double mRem = (abs - d) * 60;
        int m = (int)mRem;
        double s = (mRem - m) * 60;
        return $"{sign}{d:D2}° {m:D2}' {s:F0}\"";
    }
}
