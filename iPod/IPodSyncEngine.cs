namespace WinScrobb;

/// <summary>
/// Orchestrates loading an iPod's library + Play Counts buffer and scrobbling
/// every new play to Last.fm. Tracks last-sync time per device so reconnects
/// don't double-scrobble even if the device hasn't been re-synced through iTunes.
/// </summary>
public class IPodSyncEngine
{
    private readonly LastFmClient _client;
    private readonly Action<string> _log;

    public IPodSyncEngine(LastFmClient client, Action<string> log)
    {
        _client = client;
        _log    = log;
    }

    public record SyncSummary(int TracksOnDevice, int NewPlays, int Scrobbled, int Skipped, int Failed);

    public async Task<SyncSummary> SyncAsync(IPodDeviceInfo device, AppConfig config)
    {
        _log($"iPod {device.Name}: reading library at {device.MountPath}…");
        if (device.IsCompressed) _log("  iTunesCDB detected — attempting QuickLZ decompression.");
        _log($"  iTunesDB: {(File.Exists(device.ITunesDbPath) ? new FileInfo(device.ITunesDbPath).Length + " bytes" : "missing")}");
        _log($"  Play Counts: {(device.PlayCountsPath is null ? "missing" : new FileInfo(device.PlayCountsPath).Length + " bytes")}");
        _log($"  iTunesStats: {(device.ITunesStatsPath is null ? "missing" : new FileInfo(device.ITunesStatsPath).Length + " bytes")}");

        List<IPodTrack> tracks;
        try { tracks = ITunesDbParser.Parse(device.ITunesDbPath); }
        catch (Exception ex)
        {
            _log($"  ✗ iTunesDB parse failed: {ex.Message}");
            return new SyncSummary(0, 0, 0, 0, 0);
        }

        _log($"  Found {tracks.Count} tracks on device.");

        var  newPlays         = new List<PlayCountsParser.Entry>();
        bool usedStatsFallback = false;
        var  statsFileMtime    = DateTime.MinValue;

        // Primary: Play Counts file written by the iPod firmware
        if (device.PlayCountsPath is not null)
        {
            var rawPlays = PlayCountsParser.Parse(device.PlayCountsPath);
            if (rawPlays.Count > 0)
            {
                // Nano 3G quirk: some firmware writes lastPlayed=0 (DateTime.MinValue sentinel).
                // Substitute the file's own write-time spread across the entries so the watermark
                // remains stable — we won't re-scrobble the same plays if the device stays connected.
                var fileMtime = File.GetLastWriteTimeUtc(device.PlayCountsPath);
                int total     = rawPlays.Count;
                newPlays = rawPlays.Select((p, idx) =>
                    p.LastPlayed == DateTime.MinValue
                        ? p with { LastPlayed = fileMtime.AddSeconds(-(total - idx)) }
                        : p).ToList();

                _log($"  Parsed {newPlays.Count} plays from Play Counts.");
            }
        }

        // Fallback: iTunesStats (Nano 3G+ when Play Counts is absent)
        if (newPlays.Count == 0 && device.ITunesStatsPath is not null)
        {
            try
            {
                statsFileMtime = File.GetLastWriteTimeUtc(device.ITunesStatsPath);
                var sinceUtcForStats = config.GetLastIPodSync(device.Id);

                // Guard: only process if the file changed since our last sync.
                // This prevents re-scrobbling the same entries when the device
                // stays connected across multiple polling cycles.
                if (statsFileMtime > sinceUtcForStats)
                {
                    usedStatsFallback = true;
                    var stats = ITunesStatsParser.Parse(device.ITunesStatsPath);
                    _log($"  Parsed {stats.Count} plays from iTunesStats fallback.");

                    int idx = 0;
                    foreach (var s in stats.Where(s => s.PlayCountDelta > 0))
                    {
                        var fakeTs = statsFileMtime.AddMinutes(-(stats.Count - idx) * 4);
                        newPlays.Add(new PlayCountsParser.Entry(s.TrackIndex, fakeTs, s.PlayCountDelta, s.SkipCountDelta));
                        idx++;
                    }
                }
            }
            catch (Exception ex) { _log($"  ⚠ iTunesStats parse failed: {ex.Message}"); }
        }

        var sinceUtc = config.GetLastIPodSync(device.Id);
        var fresh    = newPlays.Where(p => p.LastPlayed > sinceUtc).ToList();

        if (fresh.Count == 0)
        {
            _log($"  No new plays since {sinceUtc:yyyy-MM-dd HH:mm} UTC.");
            return new SyncSummary(tracks.Count, 0, 0, 0, 0);
        }

        _log($"  {fresh.Count} new play(s) to scrobble.");

        int ok = 0, skip = 0, fail = 0;
        DateTime maxSeen = sinceUtc;

        foreach (var play in fresh.OrderBy(p => p.LastPlayed))
        {
            if (play.TrackIndex >= tracks.Count)
            {
                _log($"  ⚠ Play index {play.TrackIndex} out of range (only {tracks.Count} tracks)");
                skip++;
                continue;
            }

            var t = tracks[play.TrackIndex];
            if (!t.HasMetadata)
            {
                _log($"  ⚠ Track {t.TrackId} missing metadata — skipping");
                skip++;
                continue;
            }

            if (t.DurationSec > 0 && t.DurationSec < 30) { skip++; continue; }

            try
            {
                long ts = new DateTimeOffset(play.LastPlayed.ToUniversalTime()).ToUnixTimeSeconds();
                await _client.ScrobbleAsync(t.Artist, t.Title, t.Album, ts, t.DurationSec);
                _log($"  ✓ {t.Artist} — {t.Title}  @ {play.LastPlayed:yyyy-MM-dd HH:mm} UTC");
                ok++;
                if (play.LastPlayed > maxSeen) maxSeen = play.LastPlayed;
            }
            catch (Exception ex)
            {
                _log($"  ✗ Failed: {t.Artist} — {t.Title}: {ex.Message}");
                fail++;
            }
        }

        // Watermark: for iTunesStats fallback use the file mtime as the watermark so that
        // a still-connected device with an unchanged stats file isn't re-scrobbled next poll.
        if (usedStatsFallback && ok > 0)
            config.SetLastIPodSync(device.Id, statsFileMtime);
        else if (maxSeen > sinceUtc)
            config.SetLastIPodSync(device.Id, maxSeen);

        config.Save();

        _log($"iPod sync complete: {ok} scrobbled, {skip} skipped, {fail} failed.");
        return new SyncSummary(tracks.Count, fresh.Count, ok, skip, fail);
    }

    /// <summary>
    /// Quick metadata-only check — used for the popup banner play-count badge.
    /// Also checks iTunesStats when Play Counts is absent (Nano 3G+ fallback).
    /// </summary>
    public static int CountNewPlays(IPodDeviceInfo device, AppConfig config)
    {
        var since = config.GetLastIPodSync(device.Id);

        if (device.PlayCountsPath is not null)
        {
            try
            {
                var plays = PlayCountsParser.Parse(device.PlayCountsPath);
                if (plays.Count > 0)
                {
                    var fileMtime = File.GetLastWriteTimeUtc(device.PlayCountsPath);
                    return plays.Count(p =>
                    {
                        var ts = p.LastPlayed == DateTime.MinValue ? fileMtime : p.LastPlayed;
                        return ts > since && p.PlayCount > 0;
                    });
                }
            }
            catch { }
        }

        // iTunesStats fallback: if the file is newer than the last sync watermark,
        // sum up all play-count deltas as the "new plays" estimate.
        if (device.ITunesStatsPath is not null)
        {
            try
            {
                var mtime = File.GetLastWriteTimeUtc(device.ITunesStatsPath);
                if (mtime > since)
                    return ITunesStatsParser.Parse(device.ITunesStatsPath)
                                           .Sum(s => (int)s.PlayCountDelta);
            }
            catch { }
        }

        return 0;
    }
}
