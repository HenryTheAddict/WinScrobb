namespace WinScrobb;

/// <summary>
/// Finds connected iPods by scanning all drives for the standard
/// <c>iPod_Control</c> folder. Supports every model that uses the
/// iTunesDB / iTunesCDB library format (Classic, Mini, Nano, Video, Touch 1-3G).
/// </summary>
public static class IPodDetector
{
    public static List<IPodDeviceInfo> FindConnectedIPods()
    {
        var found = new List<IPodDeviceInfo>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;
            // iPods mount as Removable on most systems; some show as Fixed in
            // forced-disk-mode — accept both so no devices are missed.
            if (drive.DriveType is not (DriveType.Removable or DriveType.Fixed)) continue;

            try
            {
                var root      = drive.RootDirectory.FullName;
                var ctrlDir   = Path.Combine(root, "iPod_Control");
                var iTunesDir = Path.Combine(ctrlDir, "iTunes");
                if (!Directory.Exists(ctrlDir) || !Directory.Exists(iTunesDir)) continue;

                var dbPath        = Path.Combine(iTunesDir, "iTunesDB");
                var cdbPath       = Path.Combine(iTunesDir, "iTunesCDB");
                var playCountPath = Path.Combine(iTunesDir, "Play Counts");
                var statsPath     = Path.Combine(iTunesDir, "iTunesStats");

                bool hasDb  = File.Exists(dbPath);
                bool hasCdb = File.Exists(cdbPath);
                if (!hasDb && !hasCdb) continue;

                found.Add(new IPodDeviceInfo
                {
                    MountPath       = root,
                    Name            = TryReadDeviceName(ctrlDir, drive) ?? $"iPod ({drive.Name.TrimEnd('\\')})",
                    ITunesDbPath    = hasDb ? dbPath : cdbPath,
                    IsCompressed    = hasCdb && !hasDb,
                    PlayCountsPath  = File.Exists(playCountPath) ? playCountPath : null,
                    ITunesStatsPath = File.Exists(statsPath)     ? statsPath     : null,
                });
            }
            catch { /* drive not accessible — skip */ }
        }

        return found;
    }

    private static string? TryReadDeviceName(string ctrlDir, DriveInfo drive)
    {
        // SysInfo is a plain-text key:value file present on almost all iPods
        var sysInfo = Path.Combine(ctrlDir, "Device", "SysInfo");
        if (File.Exists(sysInfo))
        {
            try
            {
                string? modelStr = null;

                foreach (var line in File.ReadAllLines(sysInfo))
                {
                    // Prefer ModelNumStr — it contains a human-readable model name on
                    // firmware 1.0+ devices, e.g. "nano (6th generation)" or "classic".
                    if (line.StartsWith("ModelNumStr:", StringComparison.OrdinalIgnoreCase))
                    {
                        modelStr = line[12..].Trim();
                        break; // highest-priority field found
                    }
                    // Older firmware may only have "databaseID" or similar — keep scanning
                }

                if (!string.IsNullOrEmpty(modelStr))
                {
                    // Some firmware already includes "iPod" in the string; avoid doubling it.
                    return modelStr.StartsWith("iPod", StringComparison.OrdinalIgnoreCase)
                        ? modelStr
                        : $"iPod {modelStr}";
                }
            }
            catch { }
        }

        // Fallback: use the volume label if it looks like a device name
        try
        {
            var label = drive.VolumeLabel;
            if (!string.IsNullOrWhiteSpace(label) && label.Length < 40)
                return label;
        }
        catch { }

        return null;
    }
}

public record IPodDeviceInfo
{
    public required string  MountPath       { get; init; }
    public required string  Name            { get; init; }
    public required string  ITunesDbPath    { get; init; }
    public required bool    IsCompressed    { get; init; }
    public          string? PlayCountsPath  { get; init; }
    public          string? ITunesStatsPath { get; init; }

    public string Id => MountPath.TrimEnd('\\').ToUpperInvariant();
}
