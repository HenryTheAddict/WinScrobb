using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace WinScrobb;

public record UpdateInfo(Version Remote, string TagName, string DownloadUrl, string ReleaseNotes);

public static class UpdateChecker
{
    private const string ApiLatest =
        "https://api.github.com/repos/HenryTheAddict/WinScrobb/releases/latest";

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 1);

    // Returns an UpdateInfo when a newer release exists on GitHub, otherwise null.
    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("WinScrobb-Updater");
            http.Timeout = TimeSpan.FromSeconds(15);

            using var doc = JsonDocument.Parse(await http.GetStringAsync(ApiLatest));
            var root = doc.RootElement;

            var tag  = root.GetProperty("tag_name").GetString() ?? "";
            var body = root.TryGetProperty("body", out var b) ? (b.GetString() ?? "") : "";

            if (!Version.TryParse(tag.TrimStart('v'), out var remote)) return null;
            if (remote <= CurrentVersion) return null;

            // Pick first .exe asset
            string? downloadUrl = null;
            if (root.TryGetProperty("assets", out var assets))
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        downloadUrl = a.TryGetProperty("browser_download_url", out var u)
                            ? u.GetString() : null;
                        break;
                    }
                }

            return downloadUrl is null ? null : new UpdateInfo(remote, tag, downloadUrl, body);
        }
        catch { return null; }
    }

    // Downloads the installer to %TEMP%, then launches a hidden PowerShell watcher
    // that starts the installer only *after* this process has fully exited.
    // This avoids the "file in use" lock that InnoSetup hits when it tries to replace
    // a running WinScrobb.exe — the exe can't be overwritten while the OS holds it open.
    public static async Task DownloadAndInstallAsync(UpdateInfo update, IProgress<int>? progress = null)
    {
        var dest = Path.Combine(Path.GetTempPath(), $"WinScrobb-Setup-{update.TagName}.exe");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WinScrobb-Updater");
        http.Timeout = TimeSpan.FromMinutes(10);

        using var resp = await http.GetAsync(update.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();

        var total = resp.Content.Headers.ContentLength ?? -1L;

        // Explicit block so both streams are fully closed (file handles released)
        // before we hand the installer path to PowerShell.
        await using (var src  = await resp.Content.ReadAsStreamAsync())
        await using (var file = File.Create(dest))
        {
            var buf  = new byte[81920];
            long got = 0;
            int  n;
            while ((n = await src.ReadAsync(buf)) > 0)
            {
                await file.WriteAsync(buf.AsMemory(0, n));
                got += n;
                if (total > 0) progress?.Report((int)(got * 100 / total));
            }
        }

        progress?.Report(100);

        // PID-watcher script: PowerShell polls until our process is gone, then
        // runs the installer. This guarantees the exe is fully unlocked before
        // InnoSetup tries to overwrite it.
        int pid      = Process.GetCurrentProcess().Id;
        var script   = Path.Combine(Path.GetTempPath(), "WinScrobb-update.ps1");
        var destPs   = dest.Replace("'", "''");    // escape for PS1 single-quoted string
        var scriptPs = script.Replace("'", "''");

        // $$""" raw string: single { } are literal (PowerShell blocks), {{expr}} is C# interpolation
        await File.WriteAllTextAsync(script, $$"""
            $id = {{pid}}
            while (Get-Process -Id $id -ErrorAction SilentlyContinue) { Start-Sleep -Milliseconds 200 }
            Start-Sleep -Milliseconds 500
            Start-Process '{{destPs}}' -ArgumentList '/SILENT', '/RESTARTAPPLICATIONS'
            Remove-Item -LiteralPath '{{scriptPs}}' -Force -ErrorAction SilentlyContinue
            """);

        Process.Start(new ProcessStartInfo(
            "powershell.exe",
            $"-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{script}\"")
        {
            UseShellExecute = true,
        });

        // Exit after the watcher is started — the watcher detects our PID disappearing
        // and only then runs the installer, so there is no race with the file lock.
        Application.Exit();
    }
}
