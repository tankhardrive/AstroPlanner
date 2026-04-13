using System.Globalization;
using System.Text;
using AstroPlanner.Models;

namespace AstroPlanner.Services;

/// <summary>
/// Generates a self-contained HTML page that embeds Aladin Lite and draws
/// a coloured FOV rectangle for each imaging setup.
/// </summary>
public static class AladinHtmlBuilder
{
    // Auto-assigned palette — one colour per setup (cycles if > 8 setups)
    private static readonly string[] Palette =
    [
        "#4da6ff", "#ff6b45", "#45cc8a", "#d45aff",
        "#ffd24d", "#ff4d94", "#45e5ff", "#ff9f45",
    ];

    public static string Build(
        double ra, double dec,
        double fovDeg,
        string objectName,
        IReadOnlyList<ImagingSetup> setups,
        Guid? bestId)
    {
        var overlayJs  = new StringBuilder();
        var legendRows = new StringBuilder();

        for (int i = 0; i < setups.Count; i++)
        {
            var s = setups[i];
            bool isBest   = s.Id == bestId;
            string color  = Palette[i % Palette.Length];
            double lw     = isBest ? 3.5 : 1.5;

            // FOV corners in sky coordinates
            var (fovW, fovH) = FovCalculator.FovArcmin(s);
            double halfWDeg  = fovW / 60.0 / 2.0;
            double halfHDeg  = fovH / 60.0 / 2.0;
            double decRad    = dec * Math.PI / 180.0;
            double raOff     = Math.Abs(Math.Cos(decRad)) > 1e-6
                               ? halfWDeg / Math.Cos(decRad) : halfWDeg;

            // Clockwise from top-left
            (double R, double D)[] corners =
            [
                (ra - raOff, dec + halfHDeg),
                (ra + raOff, dec + halfHDeg),
                (ra + raOff, dec - halfHDeg),
                (ra - raOff, dec - halfHDeg),
            ];

            string pts = string.Join(",",
                corners.Select(c => $"[{F(c.R)},{F(c.D)}]"));

            overlayJs.AppendLine($$"""
                    {
                        const ov{{i}} = A.graphicOverlay({color:'{{color}}', lineWidth:{{F1(lw)}}});
                        aladin.addOverlay(ov{{i}});
                        ov{{i}}.add(A.polygon([{{pts}}]));
                    }
                """);

            // Legend row
            string swatchStyle = isBest
                ? $"height:3px;border:1.5px solid {color};"
                : $"height:2px;";
            string nameStyle = isBest ? "font-weight:600;color:#fff;" : "";
            string star = isBest ? "<span class='best-star'>★</span>" : "";
            legendRows.AppendLine($"""
                    <div class="setup-row">
                        <div class="swatch" style="background:{color};{swatchStyle}"></div>
                        <span class="setup-name" style="{nameStyle}">{Esc(s.Name)}{star}</span>
                        <span class="setup-fov">{FovCalculator.FormatArcmin(fovW)}×{FovCalculator.FormatArcmin(fovH)}</span>
                    </div>
                """);
        }

        if (setups.Count == 0)
        {
            legendRows.AppendLine("""
                    <div style="opacity:0.45;font-size:11px;">No setups configured</div>
                """);
        }

        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="UTF-8">
            <style>
            * { margin:0; padding:0; box-sizing:border-box; }
            body { background:#000; overflow:hidden; font-family:system-ui,sans-serif; }
            #aladin-lite-div { width:100vw; height:100vh; }
            #legend {
                position:fixed; top:12px; right:12px;
                background:rgba(8,8,20,0.88);
                border:1px solid rgba(255,255,255,0.1);
                border-radius:8px;
                padding:12px 16px;
                color:#ccc;
                font-size:12px;
                z-index:9999;
                pointer-events:none;
                min-width:210px;
            }
            #legend .title { font-size:13px; font-weight:600; color:#fff; margin-bottom:10px; }
            .setup-row { display:flex; align-items:center; gap:8px; margin:5px 0; white-space:nowrap; }
            .swatch { width:22px; flex-shrink:0; border-radius:1px; }
            .setup-name { font-size:12px; }
            .setup-fov { font-size:10px; opacity:0.45; margin-left:auto; padding-left:10px; }
            .best-star { font-size:11px; margin-left:3px; color:#ffd700; }
            </style>
            <script src="https://aladin.cds.unistra.fr/AladinLite/api/v3/latest/aladin.js"></script>
            </head>
            <body>
            <div id="aladin-lite-div"></div>
            <div id="legend">
                <div class="title">{{Esc(objectName)}}</div>
                {{legendRows}}
            </div>
            <script>
            A.init.then(() => {
                const aladin = A.aladin('#aladin-lite-div', {
                    survey:               'P/DSS2/color',
                    fov:                  {{F(fovDeg)}},
                    showReticle:          false,
                    showZoomControl:      true,
                    showFullscreenControl:false,
                    showLayersControl:    true,
                    showGotoControl:      false,
                    showFrame:            false,
                    showCooGrid:          false,
                    backgroundColor:      '#000000',
                });

                aladin.gotoRaDec({{F(ra)}}, {{F(dec)}});

            {{overlayJs}}
            });
            </script>
            </body>
            </html>
            """;
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string F(double v)  => v.ToString("F6", Inv);
    private static string F1(double v) => v.ToString("F1", Inv);

    private static string Esc(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
         .Replace("'", "&#39;").Replace("\"", "&quot;");
}
