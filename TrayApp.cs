namespace WinScrobb;

public class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _pollTimer;

    private AppConfig _config;
    private LastFmClient? _client;
    private SmtcWatcher? _watcher;
    private ScrobbleEngine? _engine;
    private readonly List<string> _log = [];

    // Current track info — shown in the tray popup
    private string? _nowPlayingTrack;
    private string? _nowPlayingArtist;
    private bool    _currentTrackLoved;
    private readonly HashSet<string> _lovedKeys = [];

    private TrayPopup? _popup;

    public TrayApp()
    {
        _config = AppConfig.Load();

        _tray = new NotifyIcon
        {
            Text    = "WinScrobb",
            Icon    = LoadTrayIcon(),
            Visible = true,
        };

        // Both left and right click show the flyout
        _tray.MouseUp += OnTrayClick;

        _pollTimer = new System.Windows.Forms.Timer
        {
            Interval = _config.PollIntervalSeconds * 1000
        };
        _pollTimer.Tick += async (_, _) => await PollAsync();

        _ = InitAsync();
    }

    // ── Init ──────────────────────────────────────────────────────────────────

    private async Task InitAsync()
    {
        try   { _watcher = await SmtcWatcher.CreateAsync(); }
        catch (Exception ex) { Log($"SMTC init failed: {ex.Message}"); }

        if (!_config.IsConfigured || !_config.IsAuthenticated)
        {
            ShowSettings();
            return;
        }

        StartScrobbling();
    }

    // ── Scrobbling ────────────────────────────────────────────────────────────

    private void StartScrobbling()
    {
        _client?.Dispose();
        _client = new LastFmClient(_config.ApiKey, _config.ApiSecret)
        {
            SessionKey = _config.SessionKey
        };
        _engine = new ScrobbleEngine(_client, Log);
        _pollTimer.Start();
        Log("Scrobbler started.");
    }

    private async Task PollAsync()
    {
        if (_engine is null || _watcher is null) return;
        try
        {
            var snapshot = await _watcher.GetSnapshotAsync();
            await _engine.TickAsync(snapshot);

            if (snapshot?.HasTrack == true && snapshot.IsMusic)
            {
                _nowPlayingTrack   = snapshot.Title;
                _nowPlayingArtist  = snapshot.Artist;
                _currentTrackLoved = _lovedKeys.Contains($"{snapshot.Artist}\0{snapshot.Title}");
                _tray.Text = Truncate($"{snapshot.Artist} — {snapshot.Title}", 63);
            }
            else
            {
                _nowPlayingTrack   = null;
                _nowPlayingArtist  = null;
                _currentTrackLoved = false;
                _tray.Text = "WinScrobb";
            }
        }
        catch (Exception ex) { Log($"Poll error: {ex.Message}"); }
    }

    // ── Tray icon click → flyout ──────────────────────────────────────────────

    private void OnTrayClick(object? sender, MouseEventArgs e)
    {
        // Close existing popup if open
        if (_popup is { Visible: true })
        {
            _popup.Close();
            return;
        }

        _popup = TrayPopup.Create(_config.Username, _nowPlayingTrack, _nowPlayingArtist, _currentTrackLoved);
        _popup.LogEntries = _log;
        _popup.SettingsRequested += (_, _) => ShowSettings();
        _popup.QuitRequested     += (_, _) => ExitApp();
        _popup.LoveToggled       += (_, _) => ToggleLove();
        _popup.FormClosed        += (_, _) => _popup = null;
        _popup.ShowNearCursor();
    }

    // ── Love toggle ───────────────────────────────────────────────────────────

    private async void ToggleLove()
    {
        if (_client is null || string.IsNullOrEmpty(_nowPlayingTrack) || string.IsNullOrEmpty(_nowPlayingArtist))
            return;

        var key = $"{_nowPlayingArtist}\0{_nowPlayingTrack}";
        try
        {
            if (_lovedKeys.Contains(key))
            {
                await _client.UnloveAsync(_nowPlayingArtist, _nowPlayingTrack);
                _lovedKeys.Remove(key);
                _currentTrackLoved = false;
                Log($"  ✕ Unloved: {_nowPlayingArtist} — {_nowPlayingTrack}");
            }
            else
            {
                await _client.LoveAsync(_nowPlayingArtist, _nowPlayingTrack);
                _lovedKeys.Add(key);
                _currentTrackLoved = true;
                Log($"  ♥ Loved: {_nowPlayingArtist} — {_nowPlayingTrack}");
            }
        }
        catch (LastFmException ex) { Log($"  ✗ Love failed: {ex.Message}"); }
    }

    // ── Settings ──────────────────────────────────────────────────────────────

    private void ShowSettings()
    {
        _pollTimer.Stop();
        using var form = new SettingsForm(_config);
        if (form.ShowDialog() == DialogResult.OK)
        {
            _config = form.Config;
            _config.ApplyStartup();
            StartScrobbling();
        }
        else if (_config.IsAuthenticated)
        {
            _pollTimer.Start();
        }
    }

    // ── Log ───────────────────────────────────────────────────────────────────

    private void Log(string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _log.Add(entry);
        if (_log.Count > 500) _log.RemoveAt(0);
        System.Diagnostics.Debug.WriteLine(entry);
    }

    // ── Exit ──────────────────────────────────────────────────────────────────

    private void ExitApp()
    {
        _pollTimer.Stop();
        _tray.Visible = false;
        _client?.Dispose();
        Application.Exit();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Icon LoadTrayIcon()
    {
        foreach (var name in new[] { "logosmall.png", "icon.ico" })
        {
            var path = FluentTheme.FindAsset(name);
            if (path is null) continue;
            try
            {
                if (name.EndsWith(".png")) return FluentTheme.PngToIcon(path);
                return new Icon(path, 16, 16);
            }
            catch { }
        }
        return SystemIcons.Application;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _tray.Dispose(); _pollTimer.Dispose(); _client?.Dispose(); }
        base.Dispose(disposing);
    }
}
