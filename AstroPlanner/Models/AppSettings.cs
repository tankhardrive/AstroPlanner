namespace AstroPlanner.Models;

public class AppSettings
{
    public List<ObservationLocation> Locations { get; set; } = [];
    public string ActiveLocationName { get; set; } = "";

    /// <summary>How often to sample the night when computing visibility (minutes).</summary>
    public int VisibilityStepMinutes { get; set; } = 15;

    public List<ImagingSetup> ImagingSetups { get; set; } = [];

    public WeatherThresholds WeatherThresholds { get; set; } = new();

    /// <summary>
    /// When true, the sky-quality factor (from Bortle class) is multiplied into the Score column.
    /// When false, sky quality is shown only in the separate Sky column.
    /// </summary>
    public bool ApplySkyQualityToScore { get; set; } = false;

    // ── Legacy fields — kept only for one-time migration from old settings files ──────────────
    public ObservationSite? Site { get; set; }
    public List<HorizonProfile>? HorizonProfiles { get; set; }
    public string? ActiveHorizonProfileName { get; set; }
    // ─────────────────────────────────────────────────────────────────────────────────────────

    public ObservationLocation GetActiveLocation()
    {
        return Locations.FirstOrDefault(l => l.Name == ActiveLocationName)
               ?? Locations.FirstOrDefault()
               ?? new ObservationLocation();
    }

    /// <summary>
    /// Called after deserialization. Converts old Site + HorizonProfiles into the new Locations list.
    /// Safe to call on a fresh (no old data) settings object — does nothing in that case.
    /// </summary>
    public void MigrateIfNeeded()
    {
        if (Locations.Count == 0)
        {
            if (Site != null)
            {
                // Migrate old site + active horizon into a single location
                var horizons = HorizonProfiles ?? [HorizonProfile.Flat()];
                var activeHorizon = horizons.FirstOrDefault(h => h.Name == ActiveHorizonProfileName)
                                   ?? horizons.FirstOrDefault()
                                   ?? HorizonProfile.Flat();

                Locations =
                [
                    new ObservationLocation
                    {
                        Name           = Site.Name,
                        LatitudeDegrees  = Site.LatitudeDegrees,
                        LongitudeDegrees = Site.LongitudeDegrees,
                        ElevationMeters  = Site.ElevationMeters,
                        TimeZoneId       = Site.TimeZoneId,
                        Horizon          = activeHorizon,
                    }
                ];
                ActiveLocationName = Site.Name;
            }
            else
            {
                // Completely fresh install — seed a sensible default
                Locations = [new ObservationLocation
                {
                    Name = "My Location",
                    TimeZoneId = TimeZoneInfo.Local.Id,
                }];
                ActiveLocationName = "My Location";
            }

            // Clear legacy fields so they don't get written back to disk
            Site                    = null;
            HorizonProfiles         = null;
            ActiveHorizonProfileName = null;
        }

        if (string.IsNullOrEmpty(ActiveLocationName))
            ActiveLocationName = Locations[0].Name;
    }
}
