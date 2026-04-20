using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AstroPlanner.Models;
using AstroPlanner.Services;

namespace AstroPlanner.ViewModels;

public partial class PlannerViewModel : ViewModelBase
{
    private readonly CatalogService _catalog;
    private readonly VisibilityService _visibility;
    private readonly CometService _comets;

    // All rows built from catalog + solar system objects
    private List<ObjectRowViewModel> _allRows = [];

    private IReadOnlyList<ImagingSetup> _setups = [];
    public IReadOnlyList<ImagingSetup> Setups
    {
        get => _setups;
        set
        {
            _setups = value;
            foreach (var row in _allRows) row.Setups = value;
        }
    }

    private AnnotationService? _annotationService;
    public AnnotationService? AnnotationService
    {
        get => _annotationService;
        set
        {
            if (_annotationService != null) _annotationService.AnnotationsChanged -= OnAnnotationsChanged;
            _annotationService = value;
            if (_annotationService != null) _annotationService.AnnotationsChanged += OnAnnotationsChanged;
            foreach (var row in _allRows) row.AnnotationService = value;
        }
    }

    private void OnAnnotationsChanged() =>
        Avalonia.Threading.Dispatcher.UIThread.Post(ApplyFilterAndSort);

    [ObservableProperty] private ObservableCollection<ObjectRowViewModel> _displayRows = [];
    [ObservableProperty] private ObjectRowViewModel? _selectedRow;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotCalculating))]
    private bool _isCalculating;
    public bool IsNotCalculating => !IsCalculating;
    [ObservableProperty] private double _calculationProgress;
    [ObservableProperty] private string _statusText = "Load a catalog to begin.";

    // ── Filters ───────────────────────────────────────────────────────────────
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private string _searchText = "";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private bool _showGalaxies = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private bool _showClusters = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private bool _showNebulae = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private bool _showPlanets = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private bool _showComets = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private bool _showStars = false;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private bool _onlyVisible = false;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private bool _onlyFavorites = false;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private bool _hideImaged = false;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private decimal _minDurationHours = 0;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private decimal _maxMagnitude = 14;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private bool _showMessier = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private bool _showCaldwell = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private bool _showNgc = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private bool _showIc = true;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    private string _selectedConstellation = "All";
    [ObservableProperty] private IReadOnlyList<string> _constellations = ["All"];

    // ── Sort ──────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _sortColumn = "Score";
    [ObservableProperty] private bool _sortAscending = false;

    public bool HasActiveFilters =>
        !ShowGalaxies || !ShowClusters || !ShowNebulae || !ShowPlanets || !ShowComets ||
        ShowStars || OnlyVisible || OnlyFavorites || HideImaged ||
        MinDurationHours > 0 || MaxMagnitude < 14 ||
        !ShowMessier || !ShowCaldwell || !ShowNgc || !ShowIc ||
        SelectedConstellation != "All" ||
        SearchText.Length > 0;

    public PlannerViewModel(CatalogService catalog, VisibilityService visibility, CometService comets)
    {
        _catalog = catalog;
        _visibility = visibility;
        _comets = comets;
        _comets.CometDataRefreshed += RebuildCometRows;
    }

    /// <summary>
    /// Pushes a Bortle class value to all rows so that sky-quality scores update immediately
    /// without re-running the full visibility calculation.
    /// </summary>
    public void SetBortleClass(int? bortle)
    {
        foreach (var row in _allRows) row.BortleClass = bortle;
        ApplyFilterAndSort();
    }

    /// <summary>
    /// Pushes the "apply sky factor to score" toggle to all rows and re-sorts.
    /// </summary>
    public void SetApplySkyToScore(bool apply)
    {
        foreach (var row in _allRows) row.ApplySkyToScore = apply;
        ApplyFilterAndSort();
    }

    // Called by MainWindowViewModel after loading
    public void Initialize()
    {
        var dsObjects = _catalog.GetAll();
        var planets = SolarSystemObject.CreateDefaults();
        var cometList = _comets.GetAll();

        _allRows = dsObjects.Select(d => new ObjectRowViewModel(d) { Setups = _setups, AnnotationService = _annotationService })
            .Concat(planets.Select(p => new ObjectRowViewModel(p) { Setups = _setups, AnnotationService = _annotationService }))
            .Concat(cometList.Select(c => new ObjectRowViewModel(c) { Setups = _setups, AnnotationService = _annotationService }))
            .ToList();

        var consts = new List<string> { "All" };
        consts.AddRange(_catalog.GetConstellations()
            .Select(ConstellationNames.Expand)
            .OrderBy(n => n));
        Constellations = consts;

        ApplyFilterAndSort();
        int cometCount = cometList.Count;
        string cometInfo = cometCount > 0 ? $", {cometCount} comets" : "";
        StatusText = $"{dsObjects.Count:N0} objects loaded{cometInfo}. Select a date and press Calculate.";
    }

    private void RebuildCometRows()
    {
        _allRows = _allRows.Where(r => r.CometSource == null).ToList();
        _allRows.AddRange(_comets.GetAll().Select(c => new ObjectRowViewModel(c) { Setups = _setups, AnnotationService = _annotationService }));
        ApplyFilterAndSort();
    }

    // ── Calculate visibility ──────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanCalculate))]
    private async Task CalculateAsync(
        (DateOnly Date, ObservationSite Site, HorizonProfile Horizon, int StepMinutes) args)
    {
        IsCalculating = true;
        CalculationProgress = 0;
        StatusText = "Computing visibility…";

        try
        {
            // Get moon position at the middle of the night
            var (darkStart, darkEnd) = AstronomyService.GetAstronomicalDarkness(args.Date, args.Site);
            var midNight = darkStart + (darkEnd - darkStart) / 2;
            var moonInfo = AstronomyService.GetMoonPosition(midNight);

            // Update solar system and comet positions at midnight for detail view
            foreach (var row in _allRows.Where(r => r.SolarSystemSource != null))
            {
                var ss = row.SolarSystemSource!;
                var (ra, dec) = AstronomyService.GetPlanetPosition(ss.BodyType, midNight);
                ss.RaDegrees = ra;
                ss.DecDegrees = dec;
            }
            foreach (var row in _allRows.Where(r => r.CometSource != null))
            {
                var comet = row.CometSource!;
                var (ra, dec) = AstronomyService.GetCometPosition(comet, midNight);
                comet.RaDegrees = ra;
                comet.DecDegrees = dec;
                comet.Magnitude = AstronomyService.GetCometMagnitude(comet, midNight);
            }

            int total = _allRows.Count;
            int done = 0;

            // Process in parallel, 4 threads max to keep UI responsive
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 4 };

            await Task.Run(() =>
            {
                Parallel.ForEach(_allRows, parallelOptions, row =>
                {
                    VisibilityWindow vis;
                    if (row.DsoSource != null)
                    {
                        vis = _visibility.ComputeDso(
                            row.DsoSource, args.Date, args.Site, args.Horizon,
                            args.StepMinutes, moonInfo);
                    }
                    else if (row.SolarSystemSource != null)
                    {
                        vis = _visibility.ComputeSolarSystem(
                            row.SolarSystemSource, args.Date, args.Site, args.Horizon,
                            args.StepMinutes, moonInfo);
                    }
                    else if (row.CometSource != null)
                    {
                        vis = _visibility.ComputeComet(
                            row.CometSource, args.Date, args.Site, args.Horizon,
                            args.StepMinutes, moonInfo);
                    }
                    else return;

                    row.Visibility = vis;

                    int d = Interlocked.Increment(ref done);
                    if (d % 200 == 0)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            CalculationProgress = (double)d / total * 100);
                    }
                });
            });

            var tz = args.Site.GetTimeZone();
            foreach (var row in _allRows) row.TimeZone = tz;

            ApplyFilterAndSort();
            int visible = _allRows.Count(r => r.Visibility.IsVisible);
            var dsLocal = TimeZoneInfo.ConvertTimeFromUtc(darkStart, tz);
            var deLocal = TimeZoneInfo.ConvertTimeFromUtc(darkEnd, tz);
            StatusText = $"{visible:N0} objects visible • darkness {dsLocal:HH:mm}–{deLocal:HH:mm} • {DisplayRows.Count:N0} shown after filters";
        }
        finally
        {
            IsCalculating = false;
            CalculationProgress = 0;
        }
    }

    private bool CanCalculate() => !IsCalculating;

    // ── Filter & Sort ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void ApplyFilterAndSort()
    {
        var filtered = _allRows.AsEnumerable();

        // Search text
        if (SearchText.Length > 0)
        {
            var q = SearchText.Trim();
            filtered = filtered.Where(r =>
                r.PrimaryName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.CatalogIds.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        // Type filters
        if (!ShowGalaxies)
            filtered = filtered.Where(r => r.DsoSource == null ||
                !IsGalaxyType(r.DsoSource.Type));
        if (!ShowClusters)
            filtered = filtered.Where(r => r.DsoSource == null ||
                !IsClusterType(r.DsoSource.Type));
        if (!ShowNebulae)
            filtered = filtered.Where(r => r.DsoSource == null ||
                !IsNebulaType(r.DsoSource.Type));
        if (!ShowPlanets)
            filtered = filtered.Where(r => r.SolarSystemSource == null);
        if (!ShowComets)
            filtered = filtered.Where(r => r.CometSource == null);
        if (!ShowStars)
            filtered = filtered.Where(r => r.DsoSource == null ||
                (r.DsoSource.Type != ObjectType.Star && r.DsoSource.Type != ObjectType.DoubleStar));

        // Magnitude — exempt planets; pass comets without a computed magnitude; filter the rest
        filtered = filtered.Where(r =>
            r.SolarSystemSource != null ||
            (r.CometSource != null && (r.CometSource.Magnitude == null || r.CometSource.Magnitude <= (double)MaxMagnitude)) ||
            (r.DsoSource != null &&
             ((r.DsoSource.MagnitudeV == null && r.DsoSource.MagnitudeB == null) ||
              r.DsoSource.DisplayMagnitude <= (double)MaxMagnitude)));

        // Catalog — if any catalog is deselected, restrict DSOs to the checked catalogs
        if (!ShowMessier || !ShowCaldwell || !ShowNgc || !ShowIc)
        {
            filtered = filtered.Where(r =>
                r.SolarSystemSource != null ||  // planets never filtered by catalog
                r.CometSource != null ||         // comets never filtered by catalog
                (ShowMessier  && r.DsoSource?.MessierNumber != null) ||
                (ShowCaldwell && r.DsoSource?.CaldwellNumber != null) ||
                (ShowNgc      && r.DsoSource?.Name.StartsWith("NGC") == true) ||
                (ShowIc       && r.DsoSource?.Name.StartsWith("IC") == true));
        }

        // Constellation
        if (SelectedConstellation != null && SelectedConstellation != "All" && SelectedConstellation.Length > 0)
            filtered = filtered.Where(r =>
                r.DsoSource != null &&
                ConstellationNames.Expand(r.DsoSource.Constellation)
                    .Equals(SelectedConstellation, StringComparison.OrdinalIgnoreCase));

        // Visibility filter
        if (OnlyVisible)
            filtered = filtered.Where(r => r.Visibility.IsVisible);

        // Annotation filters
        if (OnlyFavorites)
            filtered = filtered.Where(r => r.IsFavorite);
        if (HideImaged)
            filtered = filtered.Where(r => !r.HasBeenImaged);

        // Min duration
        if (MinDurationHours > 0)
            filtered = filtered.Where(r => r.SortDuration >= (double)(MinDurationHours * 60));

        // Sort — each column has explicit ascending and descending paths
        IOrderedEnumerable<ObjectRowViewModel> sorted;
        if (SortAscending)
        {
            sorted = SortColumn switch
            {
                "Name"      => filtered.OrderBy(r => r.PrimaryName),
                "Type"      => filtered.OrderBy(r => r.TypeDisplay),
                "Const"     => filtered.OrderBy(r => r.Constellation),
                "Magnitude" => filtered.OrderBy(r => r.SortMag),
                "Duration"  => filtered.OrderBy(r => r.SortDuration),
                "PeakAlt"   => filtered.OrderBy(r => r.SortPeakAlt),
                "PeakClr"   => filtered.OrderBy(r => r.SortClearance),
                "MoonSep"   => filtered.OrderBy(r => r.SortMoonSep),
                "Score"      => filtered.OrderBy(r => r.SortScore),
                "SkyQuality" => filtered.OrderBy(r => r.SortSkyQuality),
                "Perihelion" => filtered.OrderBy(r => r.SortPerihelion),
                "BestSetup"  => filtered.OrderBy(r => r.SortBestFill),
                "VisStart"  => filtered.OrderBy(r => r.Visibility.RiseTime ?? DateTime.MaxValue),
                _           => filtered.OrderBy(r => r.SortDuration),
            };
        }
        else
        {
            sorted = SortColumn switch
            {
                "Name"      => filtered.OrderByDescending(r => r.PrimaryName),
                "Type"      => filtered.OrderByDescending(r => r.TypeDisplay),
                "Const"     => filtered.OrderByDescending(r => r.Constellation),
                "Magnitude" => filtered.OrderByDescending(r => r.SortMag),
                "Duration"  => filtered.OrderByDescending(r => r.SortDuration),
                "PeakAlt"   => filtered.OrderByDescending(r => r.SortPeakAlt),
                "PeakClr"   => filtered.OrderByDescending(r => r.SortClearance),
                "MoonSep"   => filtered.OrderByDescending(r => r.SortMoonSep),
                "Score"      => filtered.OrderByDescending(r => r.SortScore),
                "SkyQuality" => filtered.OrderByDescending(r => r.SortSkyQuality),
                "Perihelion" => filtered.OrderByDescending(r => r.SortPerihelion),
                "BestSetup"  => filtered.OrderByDescending(r => r.SortBestFill),
                "VisStart"  => filtered.OrderByDescending(r => r.Visibility.RiseTime ?? DateTime.MinValue),
                _           => filtered.OrderByDescending(r => r.SortDuration),
            };
        }

        var results = sorted.ToList();
        DisplayRows = new ObservableCollection<ObjectRowViewModel>(results);
    }

    [RelayCommand]
    private void SortBy(string column)
    {
        if (SortColumn == column)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortColumn = column;
            // Default direction: ascending for name/type/constellation; descending for everything else
            SortAscending = column is "Name" or "Type" or "Const" or "VisStart" or "Perihelion";
        }
        ApplyFilterAndSort();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = "";
        ShowGalaxies = ShowClusters = ShowNebulae = ShowPlanets = ShowComets = true;
        ShowStars = false;
        OnlyVisible = false;
        OnlyFavorites = false;
        HideImaged = false;
        MinDurationHours = 0m;
        MaxMagnitude = 14m;
        ShowMessier = ShowCaldwell = ShowNgc = ShowIc = true;
        SelectedConstellation = "All";
        ApplyFilterAndSort();
    }

    // ── Annotation commands ───────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleFavorite()
    {
        if (SelectedRow == null || _annotationService == null) return;
        _annotationService.SetFavorite(SelectedRow.AnnotationKey,
            !_annotationService.IsFavorite(SelectedRow.AnnotationKey));
        SelectedRow.NotifyAnnotationChanged();
    }

    [RelayCommand]
    private void MarkImagedToday()
    {
        if (SelectedRow == null || _annotationService == null) return;
        _annotationService.SetImaged(SelectedRow.AnnotationKey, DateOnly.FromDateTime(DateTime.Today));
        SelectedRow.NotifyAnnotationChanged();
    }

    [RelayCommand]
    private void ClearImaged()
    {
        if (SelectedRow == null || _annotationService == null) return;
        _annotationService.SetImaged(SelectedRow.AnnotationKey, null);
        SelectedRow.NotifyAnnotationChanged();
    }

    // Filter helper predicates
    private static bool IsGalaxyType(ObjectType t) =>
        t is ObjectType.Galaxy or ObjectType.GalaxyPair or ObjectType.GalaxyTriplet or ObjectType.GalaxyGroup;

    private static bool IsClusterType(ObjectType t) =>
        t is ObjectType.OpenCluster or ObjectType.GlobularCluster or ObjectType.ClusterNebula or ObjectType.StellarAssociation;

    private static bool IsNebulaType(ObjectType t) =>
        t is ObjectType.PlanetaryNebula or ObjectType.EmissionNebula or ObjectType.ReflectionNebula
        or ObjectType.SupernovaRemnant or ObjectType.HiiRegion or ObjectType.DarkNebula
        or ObjectType.BrightNebula or ObjectType.Nebula;

    // Property change handlers to re-filter live
    partial void OnOnlyFavoritesChanged(bool value) => ApplyFilterAndSort();
    partial void OnHideImagedChanged(bool value) => ApplyFilterAndSort();
    partial void OnSearchTextChanged(string value) => ApplyFilterAndSort();
    partial void OnShowGalaxiesChanged(bool value) => ApplyFilterAndSort();
    partial void OnShowClustersChanged(bool value) => ApplyFilterAndSort();
    partial void OnShowNebulaeChanged(bool value) => ApplyFilterAndSort();
    partial void OnShowPlanetsChanged(bool value) => ApplyFilterAndSort();
    partial void OnShowCometsChanged(bool value) => ApplyFilterAndSort();
    partial void OnShowStarsChanged(bool value) => ApplyFilterAndSort();
    partial void OnOnlyVisibleChanged(bool value) => ApplyFilterAndSort();
    partial void OnMinDurationHoursChanged(decimal value) => ApplyFilterAndSort();
    partial void OnMaxMagnitudeChanged(decimal value) => ApplyFilterAndSort();
    partial void OnShowMessierChanged(bool value) => ApplyFilterAndSort();
    partial void OnShowCaldwellChanged(bool value) => ApplyFilterAndSort();
    partial void OnShowNgcChanged(bool value) => ApplyFilterAndSort();
    partial void OnShowIcChanged(bool value) => ApplyFilterAndSort();
    partial void OnSelectedConstellationChanged(string value) => ApplyFilterAndSort();
}
