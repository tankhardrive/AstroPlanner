using AstroPlanner.Models;
using AstroPlanner.Services;

namespace AstroPlanner.ViewModels;

public class FovPreviewViewModel
{
    public string Title       { get; }
    public string HtmlContent { get; }

    public FovPreviewViewModel(ObjectRowViewModel row, IReadOnlyList<ImagingSetup> setups)
    {
        double ra  = row.DsoSource?.RaDegrees  ?? row.SolarSystemSource?.RaDegrees  ?? 0;
        double dec = row.DsoSource?.DecDegrees ?? row.SolarSystemSource?.DecDegrees ?? 0;
        double? objSizeArcmin = row.DsoSource?.MajorAxisArcmin
                             ?? row.SolarSystemSource?.AngularDiameterArcmin;

        var usable = setups.Where(FovCalculator.IsUsable).ToList();
        var best   = objSizeArcmin.HasValue
                     ? FovCalculator.BestSetup(usable, objSizeArcmin.Value)
                     : null;

        // Choose image FOV: large enough to contain the biggest setup FOV + 40% margin,
        // and at least 3× the object size so there's useful context around it.
        double maxSetupArcmin = usable.Count > 0
            ? usable.Max(s => { var (w, h) = FovCalculator.FovArcmin(s); return Math.Max(w, h); })
            : 30;
        double imageFovArcmin = Math.Max(maxSetupArcmin * 1.4, (objSizeArcmin ?? 5) * 3);
        imageFovArcmin = Math.Clamp(imageFovArcmin, 5, 300);

        Title = $"FOV Preview — {row.PrimaryName}";

        HtmlContent = AladinHtmlBuilder.Build(
            ra, dec,
            imageFovArcmin / 60.0,
            row.PrimaryName,
            usable,
            best?.Id);
    }
}
