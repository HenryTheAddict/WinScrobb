namespace WinScrobb;

/// <summary>
/// Finds connected iPod Classic / Nano devices by scanning removable drives
/// for the standard <c>iPod_Control</c> folder.
/// </summary>
public static class IPodDetector
{
    public static List<IPodDeviceInfo> FindConnectedIPods()
    {
        var found = new List<IPodDeviceInfo>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;
            // iPods mount as removable disks on Windows. Some show as Fixed when
            // disk-mode is forced; accept both rather than miss devices.
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

                bool hasDb  = File.Exists(dbPath);
                bool hasCdb = File.Exists(cdbPath);
                if (!hasDb && !hasCdb) continue;

                found.Add(new IPodDeviceInfo
                {
                    MountPath        = root,
                    Name             = TryReadDeviceName(ctrlDir) ?? $"iPod ({drive.Name.TrimEnd('\\')})",
                    ITunesDbPath     = hasDb ? dbPath : cdbPath,
                    IsCompressed     = hasCdb && !hasDb,
                    PlayCountsPath   = File.Exists(playCountPath) ? playCountPath : null,
                });
            }
            catch { /* drive not accessible — skip */ }
        }

        return found;
    }

    private static string? TryReadDeviceName(string ctrlDir)
    {
        try
        {
            // SysInfo is a plain text key:value file on most iPods
            var sysInfo = Path.Combine(ctrlDir, "Device", "SysInfo");
            if (!File.Exists(sysInfo)) return null;

            foreach (var line in File.ReadAllLines(sysInfo))
            {
                if (line.StartsWith("ModelNumStr:", StringComparison.OrdinalIgnoreCase))
                    return $"iPod {line[12..].Trim()}";
            }
        }
        catch { }
        return null;
    }
}

public record IPodDeviceInfo
{
    public required string  MountPath      { get; init; }
    public required string  Name           { get; init; }
    public required string  ITunesDbPath   { get; init; }
    public required bool    IsCompressed   { get; init; }
    public          string? PlayCountsPath { get; init; }

    public string Id => MountPath.TrimEnd('\\').ToUpperInvariant();
}
