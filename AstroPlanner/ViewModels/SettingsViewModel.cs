using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AstroPlanner.Models;
using AstroPlanner.Services;

namespace AstroPlanner.ViewModels;

/// <summary>Thin wrapper used in the location list so the DataTemplate can show IsActive.</summary>
public class LocationRowViewModel(ObservationLocation location, bool isActive)
{
    public ObservationLocation Location { get; } = location;
    public bool IsActive { get; } = isActive;
    public string Name => Location.Name;
    public string HorizonSummary => Location.Horizon.Points.Count == 0 || IsFlat(Location.Horizon)
        ? "Flat (0°)"
        : $"Custom ({Location.Horizon.Points.Count} pts)";

    private static bool IsFlat(HorizonProfile h) =>
        h.Points.Count <= 3 && h.Points.All(p => p.Alt == 0);
}

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private AppSettings _settings;

    // ── Location list ────────────────────────────────────────────────────────
    [ObservableProperty] private List<LocationRowViewModel> _locationRows = [];
    [ObservableProperty] private ObservationLocation? _selectedLocation;
    public bool HasSelectedLocation => SelectedLocation != null;

    // ── Location editor fields ───────────────────────────────────────────────
    [ObservableProperty] private string _locName     = "";
    [ObservableProperty] private string _latitude    = "";
    [ObservableProperty] private string _longitude   = "";
    [ObservableProperty] private string _elevation   = "";
    [ObservableProperty] private string _timeZoneId  = "";
    [ObservableProperty] private string _locError    = "";

    // ── Horizon editor fields ────────────────────────────────────────────────
    [ObservableProperty] private string _horizonText   = "";
    [ObservableProperty] private string _horizonName   = "";
    [ObservableProperty] private string _horizonError  = "";
    [ObservableProperty] private string _horizonSummary = "Flat (0°)";

    // ── Computation ──────────────────────────────────────────────────────────
    [ObservableProperty] private decimal _stepMinutes = 15;

    // ── Imaging setups ───────────────────────────────────────────────────────
    [ObservableProperty] private List<ImagingSetupViewModel> _setupRows = [];
    [ObservableProperty] private ImagingSetupViewModel? _selectedSetup;
    [ObservableProperty] private string _setupError = "";
    public bool HasSelectedSetup => SelectedSetup != null;

    public event Action? SettingsSaved;

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _settings = settingsService.Load();
        StepMinutes = (decimal)_settings.VisibilityStepMinutes;
        RebuildRows();
        RebuildSetupRows();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void RebuildRows()
    {
        LocationRows = _settings.Locations
            .Select(l => new LocationRowViewModel(l, l.Name == _settings.ActiveLocationName))
            .ToList();
    }

    private void RebuildSetupRows()
    {
        SetupRows = _settings.ImagingSetups.Select(s => new ImagingSetupViewModel(s)).ToList();
    }

    private void PopulateEditor(ObservationLocation loc)
    {
        LocName    = loc.Name;
        Latitude   = loc.LatitudeDegrees.ToString("F4");
        Longitude  = loc.LongitudeDegrees.ToString("F4");
        Elevation  = loc.ElevationMeters.ToString("F0");
        TimeZoneId = loc.TimeZoneId;
        LocError   = "";
        HorizonText    = "";
        HorizonName    = loc.Horizon.Name == "Flat (0°)" ? "" : loc.Horizon.Name;
        HorizonError   = "";
        HorizonSummary = new LocationRowViewModel(loc, false).HorizonSummary;
    }

    partial void OnSelectedLocationChanged(ObservationLocation? value)
    {
        OnPropertyChanged(nameof(HasSelectedLocation));
        if (value != null) PopulateEditor(value);
    }

    partial void OnSelectedSetupChanged(ImagingSetupViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedSetup));
        SetupError = "";
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private void SelectLocation(ObservationLocation loc)
    {
        SelectedLocation = loc;
    }

    [RelayCommand]
    private void SetActiveLocation(ObservationLocation loc)
    {
        _settings.ActiveLocationName = loc.Name;
        _settingsService.Save(_settings);
        RebuildRows();
        SettingsSaved?.Invoke();
    }

    [RelayCommand]
    private void AddLocation()
    {
        var loc = new ObservationLocation
        {
            Name      = "New Location",
            TimeZoneId = TimeZoneInfo.Local.Id,
        };
        _settings.Locations.Add(loc);
        _settingsService.Save(_settings);
        RebuildRows();
        SelectedLocation = loc;
    }

    [RelayCommand]
    private void DeleteLocation(ObservationLocation loc)
    {
        if (_settings.Locations.Count <= 1) return; // always keep at least one
        _settings.Locations.Remove(loc);
        if (_settings.ActiveLocationName == loc.Name)
            _settings.ActiveLocationName = _settings.Locations[0].Name;
        _settingsService.Save(_settings);
        RebuildRows();
        SelectedLocation = SelectedLocation == loc ? null : SelectedLocation;
        SettingsSaved?.Invoke();
    }

    [RelayCommand]
    private void SaveLocation()
    {
        if (SelectedLocation == null) return;
        LocError = "";

        if (!double.TryParse(Latitude,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double lat)
            || lat < -90 || lat > 90)
        { LocError = "Latitude must be a number between −90 and 90."; return; }

        if (!double.TryParse(Longitude,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double lon)
            || lon < -180 || lon > 180)
        { LocError = "Longitude must be a number between −180 and 180."; return; }

        if (!double.TryParse(Elevation,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double elev))
            elev = 0;

        try { TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId.Trim()); }
        catch { LocError = "Unrecognised timezone ID. Use IANA format, e.g. America/New_York."; return; }

        string newName = string.IsNullOrWhiteSpace(LocName) ? "My Location" : LocName.Trim();

        // If the name changed and was the active location, keep active in sync
        if (_settings.ActiveLocationName == SelectedLocation.Name)
            _settings.ActiveLocationName = newName;

        SelectedLocation.Name             = newName;
        SelectedLocation.LatitudeDegrees  = lat;
        SelectedLocation.LongitudeDegrees = lon;
        SelectedLocation.ElevationMeters  = elev;
        SelectedLocation.TimeZoneId       = TimeZoneId.Trim();

        _settings.VisibilityStepMinutes = Math.Clamp((int)StepMinutes, 1, 60);
        _settingsService.Save(_settings);
        RebuildRows();
        LocError = "Saved.";
        SettingsSaved?.Invoke();
    }

    [RelayCommand]
    private void ImportHorizon()
    {
        if (SelectedLocation == null) return;
        HorizonError = "";

        if (string.IsNullOrWhiteSpace(HorizonText))
        { HorizonError = "Paste Stellarium horizon data above."; return; }

        string name = string.IsNullOrWhiteSpace(HorizonName)
            ? SelectedLocation.Name + " Horizon"
            : HorizonName.Trim();

        var profile = HorizonProfile.ParseStellariumFormat(name, HorizonText);
        if (profile.Points.Count < 2)
        { HorizonError = "Need at least 2 valid AZ ALT pairs."; return; }

        SelectedLocation.Horizon = profile;
        _settingsService.Save(_settings);
        RebuildRows();

        HorizonText    = "";
        HorizonSummary = new LocationRowViewModel(SelectedLocation, false).HorizonSummary;
        HorizonError   = $"Imported {profile.Points.Count} points successfully.";
        SettingsSaved?.Invoke();
    }

    [RelayCommand]
    private void ClearHorizon()
    {
        if (SelectedLocation == null) return;
        SelectedLocation.Horizon = HorizonProfile.Flat();
        _settingsService.Save(_settings);
        RebuildRows();
        HorizonSummary = "Flat (0°)";
        HorizonError   = "";
        SettingsSaved?.Invoke();
    }

    // ── Imaging setup commands ────────────────────────────────────────────────

    [RelayCommand]
    private void AddSetup()
    {
        var setup = new ImagingSetup { Name = "New Setup" };
        _settings.ImagingSetups.Add(setup);
        _settingsService.Save(_settings);
        RebuildSetupRows();
        SelectedSetup = SetupRows[^1];
        SettingsSaved?.Invoke();
    }

    [RelayCommand]
    private void SelectSetup(ImagingSetupViewModel vm)
    {
        SelectedSetup = vm;
    }

    [RelayCommand]
    private void DeleteSetup(ImagingSetup setup)
    {
        _settings.ImagingSetups.Remove(setup);
        _settingsService.Save(_settings);
        RebuildSetupRows();
        if (SelectedSetup?.Source == setup) SelectedSetup = null;
        SettingsSaved?.Invoke();
    }

    [RelayCommand]
    private void SaveSetup()
    {
        if (SelectedSetup == null) return;
        var error = SelectedSetup.TrySave();
        if (error != null) { SetupError = error; return; }
        _settingsService.Save(_settings);
        RebuildSetupRows();
        // Re-select the same underlying setup
        SelectedSetup = SetupRows.FirstOrDefault(r => r.Source.Id == SelectedSetup.Source.Id)
                        ?? SelectedSetup;
        SetupError = "Saved.";
        SettingsSaved?.Invoke();
    }

    public AppSettings GetCurrentSettings() => _settings;
}
