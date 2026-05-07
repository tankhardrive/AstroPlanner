using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace AstroPlanner.Services;

public record UpdateInfo(Version LatestVersion, string DownloadUrl, string TagName);

public class UpdateService
{
    private const string ApiUrl = "https://api.github.com/repos/tankhardrive/AstroPlanner/releases/latest";
    private const string ReleasesUrl = "https://github.com/tankhardrive/AstroPlanner/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("AstroPlanner");
        return c;
    }

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0);

    public static string CurrentVersionString
    {
        get
        {
            var v = CurrentVersion;
            return v.Build > 0 ? $"{v.Major}.{v.Minor}.{v.Build}" : $"{v.Major}.{v.Minor}";
        }
    }

    public async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            var json = await Http.GetStringAsync(ApiUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            if (!TryParseNormalized(tagName.TrimStart('v'), out var latest))
                return null;

            var current = new Version(CurrentVersion.Major, CurrentVersion.Minor,
                Math.Max(0, CurrentVersion.Build));
            if (latest <= current)
                return null;

            var suffix = AssetSuffix();
            string? downloadUrl = null;
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            return downloadUrl != null ? new UpdateInfo(latest, downloadUrl, tagName) : null;
        }
        catch
        {
            return null;
        }
    }

    // Returns: null (Windows — app is exiting), "restart" (Linux), "browser" (macOS), "error:..." on failure.
    public async Task<string?> DownloadAndInstallAsync(UpdateInfo info, IProgress<int>? progress = null)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start(new ProcessStartInfo(ReleasesUrl) { UseShellExecute = true });
            return "browser";
        }

        try
        {
            var fileName = Path.GetFileName(new Uri(info.DownloadUrl).LocalPath);
            var tempFile = Path.Combine(Path.GetTempPath(), fileName);

            using var response = await Http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1;
            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var file   = File.Create(tempFile);

            var buffer = new byte[81920];
            long downloaded = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read));
                downloaded += read;
                if (total > 0) progress?.Report((int)(downloaded * 100 / total));
            }
            await file.FlushAsync();
            file.Close();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });
                Environment.Exit(0);
                return null;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // $APPIMAGE is set by the AppImage runtime to the path of the running AppImage.
                var appImagePath = Environment.GetEnvironmentVariable("APPIMAGE");
                if (!string.IsNullOrEmpty(appImagePath) && File.Exists(appImagePath))
                {
                    File.Copy(tempFile, appImagePath, overwrite: true);
                    Process.Start(new ProcessStartInfo("chmod", $"+x \"{appImagePath}\"")
                        { UseShellExecute = false, CreateNoWindow = true })?.WaitForExit(3000);
                }
                else
                {
                    // Dev/non-AppImage run — drop the file next to the process
                    var dest = Path.Combine(
                        Path.GetDirectoryName(Environment.ProcessPath) ?? Path.GetTempPath(),
                        fileName);
                    File.Copy(tempFile, dest, overwrite: true);
                    Process.Start(new ProcessStartInfo("chmod", $"+x \"{dest}\"")
                        { UseShellExecute = false, CreateNoWindow = true })?.WaitForExit(3000);
                }
                File.Delete(tempFile);
                return "restart";
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"error:{ex.Message}";
        }
    }

    private static string AssetSuffix()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "-Setup.exe";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))    return "-macos.tar.gz";
        return "-x86_64.AppImage";
    }

    private static bool TryParseNormalized(string s, out Version version)
    {
        var parts = s.Split('.');
        var normalized = string.Join(".",
            parts.Concat(Enumerable.Repeat("0", Math.Max(0, 3 - parts.Length))));
        return Version.TryParse(normalized, out version!);
    }
}
