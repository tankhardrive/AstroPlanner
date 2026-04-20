using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AstroPlanner.Models;
using AstroPlanner.Services;
using AstroPlanner.Views;
using Avalonia.Media;
using Avalonia.Threading;

namespace AstroPlanner.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly WeatherService _weatherService = new();
    private readonly LightPollutionService _lightPollutionService = new();
    private readonly CometService _cometService = new();
    private readonly StellariumService _stellariumService = new();
    private AnnotationService _annotationService = null!;
    private AppSettings _settings;

    public PlannerViewModel Planner { get; }
    public ObjectDetailViewModel Detail { get; }
    public SettingsViewModel Settings { get; }
    public WeatherStripViewModel WeatherStrip { get; }

    [ObservableProperty] private DateTime? _observingDate = DateTime.Today;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotShowSettings))]
    private bool _showSettings;
    public bool IsNotShowSettings => !ShowSettings;

    public string SiteName => _settings.GetActiveLocation().Name;
    public string HorizonName => _settings.GetActiveLocation().Horizon.Name;
    public string MoonInfo { get; private set; } = "";

    public string BortleDisplay
    {
        get
        {
            int? b = _settings.GetActiveLocation().BortleClass;
            return b.HasValue ? $"B{b}" : "";
        }
    }

    public string BortleToolTip
    {
        get
        {
            int? b = _settings.GetActiveLocation().BortleClass;
            return b.HasValue
                ? SkyQuality.GetBortleLabel(b.Value) + " — click to open light pollution map"
                : "Sky quality not yet fetched — will auto-fetch on Calculate";
        }
    }

    public IBrush BortleColor
    {
        get
        {
            int? b = _settings.GetActiveLocation().BortleClass;
            if (!b.HasValue) return Brushes.Gray;
            return b.Value <= 3 ? new SolidColorBrush(Color.FromRgb(80, 200, 100))
                 : b.Value <= 5 ? new SolidColorBrush(Color.FromRgb(220, 180, 50))
                 : new SolidColorBrush(Color.FromRgb(220, 80, 60));
        }
    }

    public MainWindowViewModel()
    {
        _settingsService = new SettingsService();
        _settings = _settingsService.Load();

        _annotationService = new AnnotationService(_settingsService, _settings);
        _stellariumService.BaseUrl = _settings.StellariumUrl;

        var catalog = new CatalogService();
        var visibility = new VisibilityService();
        var images = new ImageService();

        Planner      = new PlannerViewModel(catalog, visibility, _cometService);
        Detail       = new ObjectDetailViewModel(images, visibility);
        Settings     = new SettingsViewModel(_settingsService, _cometService);
        WeatherStrip = new WeatherStripViewModel(_weatherService);

        Planner.AnnotationService    = _annotationService;
        Detail.AnnotationService     = _annotationService;
        Detail.StellariumService     = _stellariumService;

        Settings.SettingsSaved += OnSettingsSaved;
        PushSetups();

        // Wire selection: when planner row changes, update detail
        Planner.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlannerViewModel.SelectedRow))
                OpenDetail(Planner.SelectedRow);
        };

        // Load catalog then weather on background thread
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _cometService.LoadAsync();
        await Task.Run(() => Planner.Initialize());
        Dispatcher.UIThread.Post(UpdateMoonInfo);
        await RefreshWeatherAsync();
    }

    private Task RefreshWeatherAsync()
    {
        var date       = DateOnly.FromDateTime(ObservingDate ?? DateTime.Today);
        var location   = _settings.GetActiveLocation();
        var thresholds = _settings.WeatherThresholds;
        return WeatherStrip.LoadAsync(location, date, thresholds);
    }

    private void OnSettingsSaved()
    {
        _settings = _settingsService.Load();
        _annotationService.UpdateSettings(_settings);
        _stellariumService.BaseUrl = _settings.StellariumUrl;
        OnPropertyChanged(nameof(SiteName));
        OnPropertyChanged(nameof(HorizonName));
        OnPropertyChanged(nameof(BortleDisplay));
        OnPropertyChanged(nameof(BortleToolTip));
        OnPropertyChanged(nameof(BortleColor));
        PushSetups();
        Planner.SetBortleClass(_settings.GetActiveLocation().BortleClass);
        Planner.SetApplySkyToScore(_settings.ApplySkyQualityToScore);
        _ = RefreshWeatherAsync();
    }

    private void PushSetups()
    {
        var setups = _settings.ImagingSetups;
        Planner.Setups = setups;
        Detail.Setups  = setups;
    }

    private void OpenDetail(ObjectRowViewModel? row)
    {
        var loc = _settings.GetActiveLocation();
        Detail.Site = loc.ToSite();
        Detail.Horizon = loc.Horizon;
        Detail.ObservingDate = DateOnly.FromDateTime(ObservingDate ?? DateTime.Today);
        Detail.Source = row;
    }

    [RelayCommand]
    private async Task CalculateAsync()
    {
        var date = DateOnly.FromDateTime(ObservingDate ?? DateTime.Today);
        var loc = _settings.GetActiveLocation();
        var site = loc.ToSite();
        var horizon = loc.Horizon;
        int step = _settings.VisibilityStepMinutes;

        await Planner.CalculateCommand.ExecuteAsync((date, site, horizon, step));
        Planner.SetBortleClass(loc.BortleClass);
        Planner.SetApplySkyToScore(_settings.ApplySkyQualityToScore);
        UpdateMoonInfo();

        // Auto-fetch Bortle class if not yet stored for this location
        if (loc.BortleClass == null)
            _ = FetchAndSaveBortleAsync();
    }

    private async Task FetchAndSaveBortleAsync()
    {
        var loc    = _settings.GetActiveLocation();
        var bortle = await _lightPollutionService.FetchBortleClassAsync(
            loc.LatitudeDegrees, loc.LongitudeDegrees);
        if (bortle == null) return;

        loc.BortleClass = bortle;
        _settingsService.Save(_settings);
        Planner.SetBortleClass(bortle);
        Planner.SetApplySkyToScore(_settings.ApplySkyQualityToScore);

        OnPropertyChanged(nameof(BortleDisplay));
        OnPropertyChanged(nameof(BortleToolTip));
        OnPropertyChanged(nameof(BortleColor));
    }

    private void UpdateMoonInfo()
    {
        try
        {
            var utc = (ObservingDate ?? DateTime.Today).Date.AddHours(4); // ~midnight UTC
            var (_, _, illum) = AstronomyService.GetMoonPosition(utc);
            MoonInfo = $"Moon {illum:F0}%";
            OnPropertyChanged(nameof(MoonInfo));
        }
        catch { MoonInfo = ""; }
    }

    public void OpenFovPreview(ObjectRowViewModel row)
    {
        var vm = new FovPreviewViewModel(row, _settings.ImagingSetups);
        var window = new FovPreviewWindow { DataContext = vm };
        window.Show();
    }

    [RelayCommand]
    private void OpenWeatherDetail()
    {
        if (WeatherStrip.Hours.Count == 0) return;
        var location = _settings.GetActiveLocation();
        var date     = DateOnly.FromDateTime(ObservingDate ?? DateTime.Today);
        var vm       = new WeatherDetailViewModel(
            WeatherStrip.Hours,
            _settings.WeatherThresholds,
            location.Name,
            date,
            location.GetTimeZone());
        var window = new WeatherDetailWindow { DataContext = vm };
        window.Show();
    }

    [RelayCommand]
    private void ToggleSettings() => ShowSettings = !ShowSettings;

    [RelayCommand]
    private void OpenLightPollutionMap()
    {
        var loc = _settings.GetActiveLocation();
        var ic  = System.Globalization.CultureInfo.InvariantCulture;
        var url = $"https://www.lightpollutionmap.info/#zoom=11&lat={loc.LatitudeDegrees.ToString("F4", ic)}&lon={loc.LongitudeDegrees.ToString("F4", ic)}&layers=B0FFFFFFFTFFFFFFFFFF";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    }

    partial void OnObservingDateChanged(DateTime? value)
    {
        UpdateMoonInfo();
        _ = RefreshWeatherAsync();
    }
}
