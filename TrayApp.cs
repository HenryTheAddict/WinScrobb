namespace WinScrobb;

public class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly System.Windows.Forms.Timer _updateTimer;
    private readonly System.Windows.Forms.Timer _ipodTimer;

    private AppConfig _config;
    private LastFmClient? _client;
    private SmtcWatcher? _watcher;
    private ScrobbleEngine? _engine;
    private IPodSyncEngine? _ipodEngine;
    private readonly List<string> _log = [];

    // Current track info — shown in the tray popup
    private string? _nowPlayingTrack;
    private string? _nowPlayingArtist;
    private bool    _currentTrackLoved;
    private readonly HashSet<string> _lovedKeys = [];

    // Update state
    private UpdateInfo? _pendingUpdate;

    // iPod state
    private IPodDeviceInfo? _connectedIPod;
    private int _ipodNewPlayCount;
    private readonly HashSet<string> _seenIPodIds = [];
    private bool _ipodSyncing;

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

        // Check for updates every 4 hours; first check after 20 s
        _updateTimer = new System.Windows.Forms.Timer { Interval = 20_000 };
        _updateTimer.Tick += async (_, _) =>
        {
            _updateTimer.Interval = 4 * 60 * 60 * 1000; // switch to 4-hour cadence
            await CheckForUpdateAsync();
        };
        _updateTimer.Start();

        // Scan for connected iPods every 8 s
        _ipodTimer = new System.Windows.Forms.Timer { Interval = 8_000 };
        _ipodTimer.Tick += (_, _) => ScanForIPods();
        _ipodTimer.Start();

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
        _ipodEngine = new IPodSyncEngine(_client, Log);
        _pollTimer.Start();
        Log("Scrobbler started.");
    }

    // ── iPod detection ────────────────────────────────────────────────────────

    private void ScanForIPods()
    {
        if (!_config.IPodSyncEnabled || _ipodSyncing || _client is null) return;

        try
        {
            var devices  = IPodDetector.FindConnectedIPods();
            var current  = devices.FirstOrDefault();

            // Disconnect detection
            if (current is null)
            {
                if (_connectedIPod is not null)
                {
                    Log($"iPod disconnected: {_connectedIPod.Name}");
                    _connectedIPod = null;
                    _ipodNewPlayCount = 0;
                }
                return;
            }

            // Same device still connected — refresh the play count silently
            if (_connectedIPod?.Id == current.Id)
            {
                _ipodNewPlayCount = IPodSyncEngine.CountNewPlays(current, _config);
                return;
            }

            // New connection
            _connectedIPod    = current;
            _ipodNewPlayCount = IPodSyncEngine.CountNewPlays(current, _config);
            Log($"iPod connected: {current.Name} ({_ipodNewPlayCount} new plays)");

            // First-time-this-session notification
            if (_seenIPodIds.Add(current.Id))
            {
                if (_ipodNewPlayCount > 0)
                {
                    _tray.BalloonTipTitle = "iPod connected";
                    _tray.BalloonTipText  = $"{current.Name} has {_ipodNewPlayCount} new play{(_ipodNewPlayCount == 1 ? "" : "s")} — click the tray icon to scrobble.";
                    _tray.BalloonTipIcon  = ToolTipIcon.Info;
                    _tray.ShowBalloonTip(6000);
                }

                if (_config.IPodAutoSyncOnConnect && _ipodNewPlayCount > 0)
                    _ = SyncIPodAsync();
            }
        }
        catch (Exception ex) { Log($"iPod scan error: {ex.Message}"); }
    }

    private async Task SyncIPodAsync()
    {
        if (_ipodEngine is null || _connectedIPod is null || _ipodSyncing) return;

        _ipodSyncing = true;
        try
        {
            var summary = await _ipodEngine.SyncAsync(_connectedIPod, _config);
            _ipodNewPlayCount = 0;

            if (summary.Scrobbled > 0)
            {
                _tray.BalloonTipTitle = "iPod sync complete";
                _tray.BalloonTipText  = $"Scrobbled {summary.Scrobbled} play{(summary.Scrobbled == 1 ? "" : "s")} from {_connectedIPod.Name}.";
                _tray.BalloonTipIcon  = ToolTipIcon.Info;
                _tray.ShowBalloonTip(5000);
            }
        }
        catch (Exception ex) { Log($"iPod sync failed: {ex.Message}"); }
        finally { _ipodSyncing = false; }
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

    // ── Auto-update ───────────────────────────────────────────────────────────

    private async Task CheckForUpdateAsync()
    {
        try
        {
            var info = await UpdateChecker.CheckAsync();
            if (info is null) return;

            _pendingUpdate = info;
            Log($"Update available: {info.TagName}");

            _tray.BalloonTipTitle = "WinScrobb update available";
            _tray.BalloonTipText  = $"Version {info.TagName} is ready to install. Click the tray icon to update.";
            _tray.BalloonTipIcon  = ToolTipIcon.Info;
            _tray.ShowBalloonTip(8000);
        }
        catch { /* non-fatal */ }
    }

    private async void InstallUpdate()
    {
        if (_pendingUpdate is null) return;

        _pollTimer.Stop();
        _updateTimer.Stop();

        using var dlg  = new UpdateProgressForm(_pendingUpdate);
        var progress   = new Progress<int>(pct => dlg.SetProgress(pct));

        dlg.Show();

        try
        {
            await UpdateChecker.DownloadAndInstallAsync(_pendingUpdate, progress);
            // Application.Exit() is called inside DownloadAndInstallAsync — we don't reach here.
        }
        catch (Exception ex)
        {
            dlg.Close();
            MessageBox.Show($"Update failed: {ex.Message}", "WinScrobb",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            _pollTimer.Start();
            _updateTimer.Start();
        }
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

        _popup = TrayPopup.Create(
            _config.Username, _nowPlayingTrack, _nowPlayingArtist,
            _currentTrackLoved, _pendingUpdate,
            _connectedIPod, _ipodNewPlayCount);

        _popup.LogEntries         = _log;
        _popup.SettingsRequested  += (_, _) => ShowSettings();
        _popup.QuitRequested      += (_, _) => ExitApp();
        _popup.LoveToggled        += (_, _) => ToggleLove();
        _popup.UpdateRequested    += (_, _) => InstallUpdate();
        _popup.SyncIPodRequested  += (_, _) => _ = SyncIPodAsync();
        _popup.FormClosed         += (_, _) => _popup = null;
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
        _updateTimer.Stop();
        _ipodTimer.Stop();
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
        if (disposing) { _tray.Dispose(); _pollTimer.Dispose(); _updateTimer.Dispose(); _ipodTimer.Dispose(); _client?.Dispose(); }
        base.Dispose(disposing);
    }
}
