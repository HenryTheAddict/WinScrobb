using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WinScrobb;

public class TrayPopup : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE  = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE       = 38;

    public event EventHandler? SettingsRequested;
    public event EventHandler? QuitRequested;
    public event EventHandler? LoveToggled;
    public event EventHandler? UpdateRequested;
    public event EventHandler? SyncIPodRequested;
    public event EventHandler? GhostToggleRequested;
    public event EventHandler? LogoTapped;
    public event EventHandler? IPodSyncToggleRequested;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string> LogEntries { get; set; } = [];

    private const int W = 300;

    private bool _childOpen;

    private static readonly string GlyphMusic    = ""; // MusicNote
    private static readonly string GlyphSettings = ""; // Settings
    private static readonly string GlyphLog      = ""; // ViewAll
    private static readonly string GlyphQuit     = ""; // PowerButton
    private static readonly string GlyphIPod     = ""; // Devices2 (EC0E)

    private readonly DateTime? _ghostUntil;
    private readonly bool      _logoUnlocked;

    public TrayPopup(string username, string? nowPlaying, string? nowPlayingArtist,
                     bool isLoved = false, UpdateInfo? update = null,
                     IPodDeviceInfo? iPod = null, int iPodNewPlays = 0,
                     DateTime? ghostUntil = null, bool retroIconUnlocked = false,
                     bool iPodSyncEnabled = true)
    {
        _ghostUntil   = ghostUntil;
        _logoUnlocked = retroIconUnlocked;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar   = false;
        TopMost         = true;
        StartPosition   = FormStartPosition.Manual;
        BackColor       = FluentTheme.Surface;
        ForeColor       = FluentTheme.TextPrimary;
        Font            = FluentTheme.Body();

        Build(username, nowPlaying, nowPlayingArtist, isLoved, update, iPod, iPodNewPlays, iPodSyncEnabled);
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    private void Build(string username, string? nowPlaying, string? nowPlayingArtist,
                       bool isLoved, UpdateInfo? update,
                       IPodDeviceInfo? iPod, int iPodNewPlays, bool iPodSyncEnabled)
    {
        SuspendLayout();
        int y = 0;

        y = AddHeader(y, username);
        if (update is not null)         y = AddUpdateBanner(y, update);
        // Always show the iPod section if a device is connected so the
        // toggle is reachable even when sync is currently disabled.
        if (iPod is not null)           y = AddIPodBanner(y, iPod, iPodNewPlays, iPodSyncEnabled);

        y = AddNowPlaying(y, nowPlaying, nowPlayingArtist, isLoved);

        AddDivider(ref y);
        y = AddGhostRow(y);

        AddDivider(ref y);

        y = AddMenuItem(y, GlyphSettings, "Settings…",  () => { Close(); SettingsRequested?.Invoke(this, EventArgs.Empty); });
        y = AddMenuItem(y, GlyphLog,      "View Log…",  ShowLog);
        if (iPod is not null)
            y = AddMenuItem(y, GlyphIPod, "iPod…",      () => { Close(); SyncIPodRequested?.Invoke(this, EventArgs.Empty); });

        AddDivider(ref y);
        y += 6;

        y = AddMenuItem(y, GlyphQuit, "Quit", () => { Close(); QuitRequested?.Invoke(this, EventArgs.Empty); });

        y += 6;
        ClientSize = new Size(W, y);
        ResumeLayout(false);
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private int AddHeader(int y, string username)
    {
        const int h = 68;
        var panel = new Panel { BackColor = FluentTheme.Surface, Location = new Point(0, y), Size = new Size(W, h) };

        var iconAsset = (_logoUnlocked ? FluentTheme.FindAsset("retroicon.png") : null)
                        ?? FluentTheme.FindAsset("logosmall.png");
        if (iconAsset != null)
        {
            var spin = new SpinningLogo
            {
                Size      = new Size(36, 36),
                Location  = new Point(12, 16),
                LogoImage = Image.FromFile(iconAsset),
            };
            spin.LogoClicked += (_, _) => LogoTapped?.Invoke(this, EventArgs.Empty);
            panel.Controls.Add(spin);
        }

        panel.Controls.Add(new Label
        {
            Text      = "WinScrobb",
            Font      = FluentTheme.Subtitle(11f),
            ForeColor = FluentTheme.TextPrimary,
            AutoSize  = true,
            Location  = new Point(54, 14),
            BackColor = Color.Transparent,
        });
        panel.Controls.Add(new Label
        {
            Text      = string.IsNullOrEmpty(username) ? "Not signed in" : $"Signed in as {username}",
            Font      = FluentTheme.Caption(8f),
            ForeColor = FluentTheme.TextMuted,
            AutoSize  = true,
            Location  = new Point(55, 38),
            BackColor = Color.Transparent,
        });

        Controls.Add(panel);
        return y + h;
    }

    // ── Update banner ─────────────────────────────────────────────────────────

    private int AddUpdateBanner(int y, UpdateInfo update)
    {
        const int h  = 36;
        var accent   = FluentTheme.Accent;
        var bg       = Color.FromArgb(FluentTheme.IsDarkMode() ? 30 : 220,
                                      accent.R, accent.G, accent.B);

        var panel = new Panel { BackColor = bg, Location = new Point(0, y), Size = new Size(W, h) };

        panel.Controls.Add(new Label
        {
            Text      = $"  ↑  {update.TagName} available",
            Font      = FluentTheme.Body(9f),
            ForeColor = Color.White,
            AutoSize  = true,
            Location  = new Point(6, 9),
            BackColor = Color.Transparent,
        });

        var btn = new Label
        {
            Text      = "Install now →",
            Font      = new Font(FluentTheme.Body(8.5f), FontStyle.Underline),
            ForeColor = Color.White,
            AutoSize  = true,
            Cursor    = Cursors.Hand,
            BackColor = Color.Transparent,
        };
        btn.Click += (_, _) => { Close(); UpdateRequested?.Invoke(this, EventArgs.Empty); };
        panel.Controls.Add(btn);
        panel.Layout += (_, _) =>
            btn.Location = new Point(W - btn.Width - 10, (h - btn.Height) / 2);

        Controls.Add(panel);
        return y + h;
    }

    // ── iPod banner ───────────────────────────────────────────────────────────

    private int AddIPodBanner(int y, IPodDeviceInfo iPod, int newPlays, bool syncEnabled)
    {
        const int h   = 48;
        var isDk      = FluentTheme.IsDarkMode();
        var bg        = syncEnabled
            ? (isDk ? Color.FromArgb(38, 42, 50) : Color.FromArgb(232, 236, 244))
            : (isDk ? Color.FromArgb(36, 36, 36) : Color.FromArgb(242, 242, 242));

        var panel = new Panel { BackColor = bg, Location = new Point(0, y), Size = new Size(W, h) };

        // Device glyph
        panel.Controls.Add(new Label
        {
            Text      = GlyphIPod,
            Font      = new Font("Segoe MDL2 Assets", 12f),
            ForeColor = syncEnabled ? FluentTheme.Accent : FluentTheme.TextMuted,
            AutoSize  = true,
            Location  = new Point(14, 15),
            BackColor = Color.Transparent,
        });

        panel.Controls.Add(new Label
        {
            Text      = Truncate(iPod.Name, 24),
            Font      = FluentTheme.Body(9f),
            ForeColor = syncEnabled ? FluentTheme.TextPrimary : FluentTheme.TextMuted,
            AutoSize  = true,
            Location  = new Point(40, 7),
            BackColor = Color.Transparent,
        });

        string subText;
        if (!syncEnabled)
            subText = "sync disabled";
        else if (newPlays == 0)
            subText = iPod.IsCompressed ? "compressed library — open to sync" : "no new plays";
        else
            subText = $"{newPlays} new play{(newPlays == 1 ? "" : "s")}";

        panel.Controls.Add(new Label
        {
            Text      = subText,
            Font      = FluentTheme.Caption(8f),
            ForeColor = FluentTheme.TextMuted,
            AutoSize  = true,
            Location  = new Point(40, 27),
            BackColor = Color.Transparent,
        });

        // Toggle (on/off) on the right side
        bool syncing = syncEnabled;
        var toggleLbl = new Label
        {
            Text      = syncEnabled ? "On" : "Off",
            Font      = FluentTheme.Caption(8f),
            ForeColor = syncEnabled ? FluentTheme.Accent : FluentTheme.TextMuted,
            AutoSize  = true,
            Cursor    = Cursors.Hand,
            BackColor = Color.Transparent,
        };
        toggleLbl.Click += (_, _) =>
        {
            Close();
            IPodSyncToggleRequested?.Invoke(this, EventArgs.Empty);
        };
        panel.Controls.Add(toggleLbl);

        // Open-menu link (only shown when sync is on)
        if (syncEnabled)
        {
            var openBtn = new Label
            {
                Text      = newPlays > 0 ? "Sync →" : "Open →",
                Font      = new Font(FluentTheme.Body(8.5f), FontStyle.Underline),
                ForeColor = FluentTheme.Accent,
                AutoSize  = true,
                Cursor    = Cursors.Hand,
                BackColor = Color.Transparent,
            };
            openBtn.Click += (_, _) => { Close(); SyncIPodRequested?.Invoke(this, EventArgs.Empty); };
            panel.Controls.Add(openBtn);

            panel.Layout += (_, _) =>
            {
                openBtn.Location   = new Point(W - openBtn.Width - 12, (h - openBtn.Height) / 2);
                toggleLbl.Location = new Point(openBtn.Left - toggleLbl.Width - 10, (h - toggleLbl.Height) / 2);
            };

            panel.Cursor = Cursors.Hand;
            panel.Click += (_, _) => { Close(); SyncIPodRequested?.Invoke(this, EventArgs.Empty); };
        }
        else
        {
            panel.Layout += (_, _) =>
                toggleLbl.Location = new Point(W - toggleLbl.Width - 12, (h - toggleLbl.Height) / 2);
        }

        Controls.Add(panel);
        return y + h;
    }

    // ── Ghost mode row ────────────────────────────────────────────────────────

    private int AddGhostRow(int y)
    {
        const int h  = 50;
        bool active  = _ghostUntil.HasValue && _ghostUntil.Value > DateTime.UtcNow;
        var isDk     = FluentTheme.IsDarkMode();

        var bg = active
            ? (isDk ? Color.FromArgb(54, 38, 70) : Color.FromArgb(232, 220, 244))
            : FluentTheme.Surface;

        var panel = new Panel { BackColor = bg, Location = new Point(0, y), Size = new Size(W, h), Cursor = Cursors.Hand };
        panel.Click += (_, _) => { Close(); GhostToggleRequested?.Invoke(this, EventArgs.Empty); };

        var glyph = new Label
        {
            Text      = "👻",
            Font      = new Font("Segoe UI Emoji", 14f),
            ForeColor = active ? Color.FromArgb(196, 160, 232) : FluentTheme.TextMuted,
            AutoSize  = true,
            Location  = new Point(14, 12),
            BackColor = Color.Transparent,
        };
        glyph.Click += (_, _) => { Close(); GhostToggleRequested?.Invoke(this, EventArgs.Empty); };
        panel.Controls.Add(glyph);

        var title = new Label
        {
            Text      = active ? "Ghost mode is on" : "Activate Ghost Mode",
            Font      = FluentTheme.Body(9.5f),
            ForeColor = active ? Color.FromArgb(196, 160, 232) : FluentTheme.TextPrimary,
            AutoSize  = true,
            Location  = new Point(48, 7),
            BackColor = Color.Transparent,
        };
        title.Click += (_, _) => { Close(); GhostToggleRequested?.Invoke(this, EventArgs.Empty); };
        panel.Controls.Add(title);

        string subText;
        if (active)
        {
            var rem = _ghostUntil!.Value - DateTime.UtcNow;
            subText = rem.TotalHours >= 1
                ? $"hides activity for {(int)rem.TotalHours}h {rem.Minutes:00}m more — tap to disable"
                : $"hides activity for {rem.Minutes}m {rem.Seconds:00}s more — tap to disable";
        }
        else
        {
            subText = "Pause scrobbling for 6 hours";
        }

        var sub = new Label
        {
            Text      = subText,
            Font      = FluentTheme.Caption(8f),
            ForeColor = FluentTheme.TextMuted,
            AutoSize  = true,
            Location  = new Point(48, 28),
            BackColor = Color.Transparent,
        };
        sub.Click += (_, _) => { Close(); GhostToggleRequested?.Invoke(this, EventArgs.Empty); };
        panel.Controls.Add(sub);

        Controls.Add(panel);
        return y + h;
    }

    // ── Now Playing ───────────────────────────────────────────────────────────

    private int AddNowPlaying(int y, string? track, string? artist, bool isLoved)
    {
        bool hasTrack = !string.IsNullOrWhiteSpace(track);
        int  h        = hasTrack ? 62 : 42;
        var  panelBg  = FluentTheme.IsDarkMode() ? Color.FromArgb(46, 46, 46) : Color.FromArgb(237, 237, 237);

        var panel = new Panel { BackColor = panelBg, Location = new Point(0, y), Size = new Size(W, h) };

        panel.Controls.Add(new Label
        {
            Text      = GlyphMusic,
            Font      = new Font("Segoe MDL2 Assets", 13f),
            ForeColor = hasTrack ? FluentTheme.Accent : FluentTheme.TextMuted,
            AutoSize  = true,
            Location  = new Point(14, hasTrack ? 14 : 11),
            BackColor = Color.Transparent,
        });

        if (hasTrack)
        {
            panel.Controls.Add(new Label
            {
                Text      = Truncate(track!, 28),
                Font      = FluentTheme.Body(9.5f),
                ForeColor = FluentTheme.TextPrimary,
                AutoSize  = true,
                Location  = new Point(44, 14),
                BackColor = Color.Transparent,
            });
            panel.Controls.Add(new Label
            {
                Text      = Truncate(artist ?? "", 34),
                Font      = FluentTheme.Caption(8f),
                ForeColor = FluentTheme.TextMuted,
                AutoSize  = true,
                Location  = new Point(45, 37),
                BackColor = Color.Transparent,
            });

            bool loved = isLoved;
            var heart = new Label
            {
                Text      = loved ? "♥" : "♡",
                Font      = new Font("Segoe UI Symbol", 14f),
                ForeColor = loved ? Color.FromArgb(232, 64, 87) : FluentTheme.TextMuted,
                AutoSize  = false,
                Size      = new Size(30, 30),
                Location  = new Point(W - 36, (h - 30) / 2),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor    = Cursors.Hand,
                BackColor = Color.Transparent,
            };
            heart.Click += (_, _) =>
            {
                loved           = !loved;
                heart.Text      = loved ? "♥" : "♡";
                heart.ForeColor = loved ? Color.FromArgb(232, 64, 87) : FluentTheme.TextMuted;
                LoveToggled?.Invoke(this, EventArgs.Empty);
            };
            panel.Controls.Add(heart);
        }
        else
        {
            panel.Controls.Add(new Label
            {
                Text      = "Nothing playing",
                Font      = FluentTheme.Body(9.5f),
                ForeColor = FluentTheme.TextMuted,
                AutoSize  = true,
                Location  = new Point(44, 11),
                BackColor = Color.Transparent,
            });
        }

        Controls.Add(panel);
        return y + h;
    }

    // ── Menu item ─────────────────────────────────────────────────────────────

    private int AddMenuItem(int y, string glyph, string label, Action action)
    {
        var item = new TrayMenuItem
        {
            Text      = label,
            Glyph     = glyph,
            Font      = FluentTheme.Body(9.5f),
            Location  = new Point(0, y),
            Size      = new Size(W, 40),
            BackColor = FluentTheme.Surface,
        };
        item.Click += (_, _) => action();
        Controls.Add(item);
        return y + 40;
    }

    // ── Divider ───────────────────────────────────────────────────────────────

    private void AddDivider(ref int y)
    {
        Controls.Add(new Panel { BackColor = FluentTheme.Divider, Location = new Point(0, y), Size = new Size(W, 1) });
        y += 1;
    }

    // ── Log viewer ────────────────────────────────────────────────────────────

    private void ShowLog()
    {
        _childOpen = true;
        Close();
        using var dlg = new LogViewerForm(LogEntries);
        dlg.ShowDialog();
        _childOpen = false;
    }

    // ── DWM chrome ────────────────────────────────────────────────────────────

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int corner = 2; DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
        int dark   = FluentTheme.IsDarkMode() ? 1 : 0;
        DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        int mica   = 2; DwmSetWindowAttribute(Handle, DWMWA_SYSTEMBACKDROP_TYPE, ref mica, sizeof(int));
    }

    protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); if (!_childOpen) Close(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!FluentTheme.IsDarkMode())
        {
            using var pen = new Pen(Color.FromArgb(200, 200, 200), 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    // ── Positioning ───────────────────────────────────────────────────────────

    public static TrayPopup Create(string username, string? nowPlaying, string? nowPlayingArtist,
                                   bool isLoved = false, UpdateInfo? update = null,
                                   IPodDeviceInfo? iPod = null, int iPodNewPlays = 0,
                                   DateTime? ghostUntil = null, bool retroIconUnlocked = false,
                                   bool iPodSyncEnabled = true)
        => new(username, nowPlaying, nowPlayingArtist, isLoved, update, iPod, iPodNewPlays,
               ghostUntil, retroIconUnlocked, iPodSyncEnabled);

    public void ShowNearCursor()
    {
        var cursor = Cursor.Position;
        var screen = Screen.FromPoint(cursor);
        int x = Math.Max(screen.WorkingArea.Left + 8,
            Math.Min(cursor.X - Width / 2, screen.WorkingArea.Right - Width - 8));
        int y = screen.WorkingArea.Bottom - Height - 20;
        Location = new Point(x, y);
        Show();
        Activate();
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
