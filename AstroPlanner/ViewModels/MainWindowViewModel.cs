using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AstroPlanner.Models;
using AstroPlanner.Services;

namespace AstroPlanner.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private AppSettings _settings;

    public PlannerViewModel Planner { get; }
    public ObjectDetailViewModel Detail { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty] private DateTime? _observingDate = DateTime.Today;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotShowSettings))]
    private bool _showSettings;
    public bool IsNotShowSettings => !ShowSettings;

    public string SiteName => _settings.GetActiveLocation().Name;
    public string HorizonName => _settings.GetActiveLocation().Horizon.Name;
    public string MoonInfo { get; private set; } = "";

    public MainWindowViewModel()
    {
        _settingsService = new SettingsService();
        _settings = _settingsService.Load();

        var catalog = new CatalogService();
        var visibility = new VisibilityService();
        var images = new ImageService();

        Planner = new PlannerViewModel(catalog, visibility);
        Detail = new ObjectDetailViewModel(images, visibility);
        Settings = new SettingsViewModel(_settingsService);

        Settings.SettingsSaved += OnSettingsSaved;

        // Wire selection: when planner row changes, update detail
        Planner.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlannerViewModel.SelectedRow))
                OpenDetail(Planner.SelectedRow);
        };

        // Load catalog on background thread so UI shows immediately
        _ = Task.Run(() =>
        {
            Planner.Initialize();
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateMoonInfo);
        });
    }

    private void OnSettingsSaved()
    {
        _settings = _settingsService.Load();
        OnPropertyChanged(nameof(SiteName));
        OnPropertyChanged(nameof(HorizonName));
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
        UpdateMoonInfo();
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

    [RelayCommand]
    private void ToggleSettings() => ShowSettings = !ShowSettings;

    partial void OnObservingDateChanged(DateTime? value)
    {
        UpdateMoonInfo();
    }
}
