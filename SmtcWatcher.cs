using Windows.Media;
using Windows.Media.Control;

namespace WinScrobb;

public record MediaSnapshot(
    string Title,
    string Artist,
    string Album,
    bool IsPlaying,
    double DurationSeconds,
    double PositionSeconds,
    MediaPlaybackType? PlaybackType,
    string SourceApp)
{
    public string TrackKey => $"{Artist}\0{Title}";
    public bool HasTrack   => !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Artist);

    // Album title is the most reliable cross-source signal that something is music.
    // Every SMTC-aware music player (Spotify, foobar2000, WMP, Apple Music) sets it.
    // YouTube, Twitch, and podcast players never set it.
    private bool HasAlbum => !string.IsNullOrWhiteSpace(Album);

    // Browser process/AUMID matching — cast wide to catch Edge, Chrome, Firefox variants.
    private bool SourceIsBrowser =>
        SourceApp.Contains("chrome",   StringComparison.OrdinalIgnoreCase) ||
        SourceApp.Contains("msedge",   StringComparison.OrdinalIgnoreCase) ||
        SourceApp.Contains("edge",     StringComparison.OrdinalIgnoreCase) ||
        SourceApp.Contains("firefox",  StringComparison.OrdinalIgnoreCase) ||
        SourceApp.Contains("mozilla",  StringComparison.OrdinalIgnoreCase) ||
        SourceApp.Contains("opera",    StringComparison.OrdinalIgnoreCase) ||
        SourceApp.Contains("brave",    StringComparison.OrdinalIgnoreCase) ||
        SourceApp.Contains("vivaldi",  StringComparison.OrdinalIgnoreCase) ||
        SourceApp.Contains("helium",   StringComparison.OrdinalIgnoreCase) ||
        SourceApp.Contains("iexplore", StringComparison.OrdinalIgnoreCase);

    public bool IsMusic
    {
        get
        {
            if (!HasTrack) return false;

            return PlaybackType switch
            {
                // Hard no for explicit non-music types
                MediaPlaybackType.Video => false,
                MediaPlaybackType.Image => false,

                // Music type from a native app → trust it
                MediaPlaybackType.Music when !SourceIsBrowser => true,

                // Music type from a browser → browsers report Music for podcasts/YouTube too,
                // so require an album to confirm it's actually a music track
                MediaPlaybackType.Music => HasAlbum,

                // Unknown type from any source → album required; no album = likely video/podcast
                _ => HasAlbum,
            };
        }
    }
}

public class SmtcWatcher : IAsyncDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public static async Task<SmtcWatcher> CreateAsync()
    {
        var w = new SmtcWatcher();
        w._manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        return w;
    }

    public async Task<MediaSnapshot?> GetSnapshotAsync()
    {
        // iTunes COM must be called before any await — ThreadPool threads are MTA
        // and COM calls to iTunes (STA server) will silently fail on MTA threads.
        var iTunes = iTunesSource.GetSnapshot();

        var smtc = await GetSmtcSnapshotAsync();

        // Prefer SMTC if it has an active music track (covers Apple Music native SMTC)
        if (smtc is { HasTrack: true, IsMusic: true, IsPlaying: true }) return smtc;

        // iTunes playing → prefer it
        if (iTunes is { HasTrack: true, IsPlaying: true }) return iTunes;

        // Paused SMTC music track beats paused iTunes
        if (smtc is { HasTrack: true, IsMusic: true }) return smtc;

        // Paused iTunes
        if (iTunes is { HasTrack: true }) return iTunes;

        return smtc;
    }

    private async Task<MediaSnapshot?> GetSmtcSnapshotAsync()
    {
        if (_manager is null) return null;

        await _lock.WaitAsync();
        try
        {
            var session = _manager.GetCurrentSession();
            if (session is null) return null;

            var props = await session.TryGetMediaPropertiesAsync();
            if (props is null) return null;

            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();

            bool isPlaying = playback?.PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            MediaPlaybackType? playbackType = null;
            try { playbackType = playback?.PlaybackType; } catch { }

            string sourceApp = "";
            try { sourceApp = session.SourceAppUserModelId ?? ""; } catch { }

            double duration = 0, position = 0;
            if (timeline is not null)
            {
                try
                {
                    duration = timeline.EndTime.TotalSeconds;
                    position = timeline.Position.TotalSeconds;
                }
                catch { }
            }

            return new MediaSnapshot(
                Title: props.Title ?? "",
                Artist: props.Artist ?? "",
                Album: props.AlbumTitle ?? "",
                IsPlaying: isPlaying,
                DurationSeconds: Math.Max(0, duration),
                PositionSeconds: Math.Max(0, position),
                PlaybackType: playbackType,
                SourceApp: sourceApp);
        }
        catch
        {
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _lock.Dispose();
        await Task.CompletedTask;
    }
}
