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
        var setupsJs   = new StringBuilder();
        var legendRows = new StringBuilder();

        for (int i = 0; i < setups.Count; i++)
        {
            var s = setups[i];
            bool isBest  = s.Id == bestId;
            string color = Palette[i % Palette.Length];
            double lw    = isBest ? 3.5 : 1.5;

            var (fovW, fovH) = FovCalculator.FovArcmin(s);

            // Bake setup data as a JS object — rotation math happens in JS
            if (i > 0) setupsJs.Append(',');
            setupsJs.Append($$"""
                {fovW:{{F(fovW)}},fovH:{{F(fovH)}},color:'{{color}}',lineWidth:{{F1(lw)}}}
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
            window.onerror = function(msg, src, line) {
                var d = document.getElementById('aladin-lite-div');
                if (d) d.innerHTML = '<pre style="color:#f66;padding:20px;font-size:12px;white-space:pre-wrap">JS Error: ' + msg + '\n' + src + ':' + line + '</pre>';
                return false;
            };

            const CENTER_RA  = {{F(ra)}};
            const CENTER_DEC = {{F(dec)}};
            const SETUPS = [{{setupsJs}}];

            let aladinInst = null;
            let overlays   = [];

            // Build rotated corner polygon for one setup in sky coords.
            // Rotation is in the tangent plane: positive = counter-clockwise
            // as viewed on the sky (north up, east left) — i.e. PA increases east of north.
            function buildCorners(fovW, fovH, rotDeg) {
                const halfW    = fovW / 60.0 / 2.0;   // degrees
                const halfH    = fovH / 60.0 / 2.0;
                const decRad   = CENTER_DEC * Math.PI / 180.0;
                const cosDec   = Math.abs(Math.cos(decRad)) > 1e-6 ? Math.cos(decRad) : 1e-6;
                const rotRad   = rotDeg * Math.PI / 180.0;
                const cosR = Math.cos(rotRad), sinR = Math.sin(rotRad);
                // Unrotated flat corners: x = RA*cos(dec) offset (east), y = Dec offset (north)
                return [[-halfW,+halfH],[+halfW,+halfH],[+halfW,-halfH],[-halfW,-halfH]]
                    .map(([x,y]) => [
                        CENTER_RA  + (x*cosR - y*sinR) / cosDec,
                        CENTER_DEC + (x*sinR + y*cosR)
                    ]);
            }

            // Called from C# via InvokeScript to rotate all FOV boxes together.
            function setRotation(deg) {
                if (!aladinInst) return;
                overlays.forEach(ov => aladinInst.removeLayer(ov));
                overlays = [];
                SETUPS.forEach(s => {
                    const ov = A.graphicOverlay({color: s.color, lineWidth: s.lineWidth});
                    aladinInst.addOverlay(ov);
                    ov.add(A.polygon(buildCorners(s.fovW, s.fovH, deg)));
                    overlays.push(ov);
                });
            }

            if (typeof A === 'undefined') {
                document.getElementById('aladin-lite-div').innerHTML =
                    '<pre style="color:#f66;padding:20px;font-size:12px">Aladin JS failed to load. Check network connectivity.</pre>';
            } else {
                A.init.then(() => {
                    aladinInst = A.aladin('#aladin-lite-div', {
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
                    aladinInst.gotoRaDec(CENTER_RA, CENTER_DEC);
                    setRotation(0);
                }).catch(err => {
                    document.getElementById('aladin-lite-div').innerHTML =
                        '<pre style="color:#f66;padding:20px;font-size:12px">Aladin init failed: ' + err + '</pre>';
                });
            }
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
