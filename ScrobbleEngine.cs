namespace WinScrobb;

/// <summary>
/// Tracks play time for the current session and fires scrobbles at the right threshold.
/// Last.fm rules: track must be >= 30s long; scrobble after 50% played or 4 minutes, whichever first.
/// </summary>
public class ScrobbleEngine(LastFmClient client, Action<string> log)
{
    private const double MinTrackLength = 30;
    private const double MaxScrobbleSeconds = 240; // 4 minutes

    private record TrackState(
        MediaSnapshot Snapshot,
        long StartedAtUnix,
        double ScrobbleThreshold);

    private TrackState? _current;
    private double _playSeconds;
    private bool _nowPlayingSent;
    private bool _scrobbled;
    private DateTime _lastTick = DateTime.UtcNow;

    private string? _lastBlockedKey;

    public async Task TickAsync(MediaSnapshot? snapshot)
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastTick).TotalSeconds;
        _lastTick = now;

        // Treat non-music as if nothing is playing
        if (snapshot is not null && !snapshot.IsMusic)
        {
            var blockedKey = snapshot.TrackKey;
            if (blockedKey != _lastBlockedKey)
            {
                var src = ShortSource(snapshot.SourceApp);
                log($"⊘ {snapshot.Artist} — {snapshot.Title}  ({src})");
                _lastBlockedKey = blockedKey;
            }
            snapshot = null;
        }
        else
        {
            _lastBlockedKey = null;
        }

        // --- Detect track change ---
        var newKey = snapshot?.HasTrack == true ? snapshot.TrackKey : null;
        var oldKey = _current?.Snapshot.TrackKey;

        if (newKey != oldKey)
        {
            if (_current is not null && !_scrobbled && _playSeconds >= MinTrackLength)
                await TryScrobbleAsync(_current);

            if (snapshot is not null && snapshot.HasTrack)
            {
                _current = new TrackState(
                    snapshot,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    ScrobbleThreshold(snapshot.DurationSeconds));
                _playSeconds = 0;
                _nowPlayingSent = false;
                _scrobbled = false;

                log($"Now playing: {snapshot.Artist} — {snapshot.Title}" +
                    (string.IsNullOrEmpty(snapshot.Album) ? "" : $" [{snapshot.Album}]"));
            }
            else
            {
                _current = null;
            }
        }

        // --- Accumulate time and fire API calls ---
        if (_current is null || snapshot is null) return;

        if (snapshot.IsPlaying)
        {
            _playSeconds += elapsed;

            if (!_nowPlayingSent)
                await TryNowPlayingAsync(_current);

            if (!_scrobbled && _playSeconds >= _current.ScrobbleThreshold)
                await TryScrobbleAsync(_current);
        }
    }

    private static double ScrobbleThreshold(double duration) =>
        duration > 0 ? Math.Min(duration / 2, MaxScrobbleSeconds) : MaxScrobbleSeconds;

    /// <summary>
    /// Reduce SMTC AUMIDs (e.g. "Helium.XXD5WR4HHS7TQABFM2HDBXBUWE!App") down to
    /// a friendly process-ish name for logs. Falls back to the executable name
    /// when an AUMID isn't present.
    /// </summary>
    private static string ShortSource(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "unknown";
        // Strip "App!ID" suffix
        var bang = raw.IndexOf('!');
        var s = bang > 0 ? raw[..bang] : raw;
        // Take first dotted segment ("Helium.XXX..." → "Helium")
        var dot = s.IndexOf('.');
        if (dot > 0) s = s[..dot];
        // Strip ".exe" if present
        if (s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) s = s[..^4];
        return s;
    }

    private async Task TryNowPlayingAsync(TrackState state)
    {
        try
        {
            var s = state.Snapshot;
            await client.UpdateNowPlayingAsync(s.Artist, s.Title, s.Album, (int)s.DurationSeconds);
            _nowPlayingSent = true;
            log("  → Now-playing sent");
        }
        catch (LastFmException ex)
        {
            log($"  ✗ Now-playing failed: {ex.Message}");
        }
    }

    private async Task TryScrobbleAsync(TrackState state)
    {
        try
        {
            var s = state.Snapshot;
            await client.ScrobbleAsync(s.Artist, s.Title, s.Album, state.StartedAtUnix, (int)s.DurationSeconds);
            _scrobbled = true;
            log($"  ✓ Scrobbled: {s.Artist} — {s.Title}");
        }
        catch (LastFmException ex)
        {
            log($"  ✗ Scrobble failed: {ex.Message}");
        }
    }
}
