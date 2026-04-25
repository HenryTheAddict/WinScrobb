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
        if (device.IsCompressed)
        {
            _log($"iPod {device.Name}: iTunesCDB (compressed) format not yet supported.");
            return new SyncSummary(0, 0, 0, 0, 0);
        }

        _log($"iPod {device.Name}: reading library at {device.MountPath}…");

        List<IPodTrack> tracks;
        try { tracks = ITunesDbParser.Parse(device.ITunesDbPath); }
        catch (Exception ex)
        {
            _log($"  ✗ iTunesDB parse failed: {ex.Message}");
            return new SyncSummary(0, 0, 0, 0, 0);
        }

        _log($"  Found {tracks.Count} tracks on device.");

        // Read Play Counts (the file the iPod itself wrote since last iTunes sync)
        var newPlays = device.PlayCountsPath is null
            ? []
            : PlayCountsParser.Parse(device.PlayCountsPath);

        // De-dupe against our own last-sync watermark (per-device)
        var sinceUtc = config.GetLastIPodSync(device.Id);
        var fresh = newPlays.Where(p => p.LastPlayed > sinceUtc).ToList();

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

            // Last.fm rejects scrobbles for tracks shorter than 30 s
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

        // Save watermark so we don't re-scrobble these next time
        config.SetLastIPodSync(device.Id, maxSeen);
        config.Save();

        _log($"iPod sync complete: {ok} scrobbled, {skip} skipped, {fail} failed.");
        return new SyncSummary(tracks.Count, fresh.Count, ok, skip, fail);
    }

    /// <summary>Quick metadata-only check — useful for the popup banner.</summary>
    public static int CountNewPlays(IPodDeviceInfo device, AppConfig config)
    {
        if (device.PlayCountsPath is null) return 0;
        try
        {
            var since = config.GetLastIPodSync(device.Id);
            return PlayCountsParser.Parse(device.PlayCountsPath).Count(p => p.LastPlayed > since);
        }
        catch { return 0; }
    }
}
