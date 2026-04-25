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

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string> LogEntries { get; set; } = [];

    private const int W = 300;

    // Set to true while a child dialog is open so OnDeactivate doesn't close the popup
    private bool _childOpen;

    // Segoe MDL2 Assets codepoints kept as string literals so the editor
    // can't mangle them — use explicit \u escapes throughout this file.
    private static readonly string GlyphMusic    = ""; // MusicNote
    private static readonly string GlyphSettings = ""; // Settings
    private static readonly string GlyphLog      = ""; // ViewAll
    private static readonly string GlyphQuit     = ""; // PowerButton

    public TrayPopup(string username, string? nowPlaying, string? nowPlayingArtist, bool isLoved = false)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar   = false;
        TopMost         = true;
        StartPosition   = FormStartPosition.Manual;
        BackColor       = FluentTheme.Surface;
        ForeColor       = FluentTheme.TextPrimary;
        Font            = FluentTheme.Body();

        Build(username, nowPlaying, nowPlayingArtist, isLoved);
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    private void Build(string username, string? nowPlaying, string? nowPlayingArtist, bool isLoved)
    {
        SuspendLayout();
        int y = 0;

        y = AddHeader(y, username);
        y = AddNowPlaying(y, nowPlaying, nowPlayingArtist, isLoved);

        AddDivider(ref y);

        y = AddMenuItem(y, GlyphSettings, "Settings…", () => { Close(); SettingsRequested?.Invoke(this, EventArgs.Empty); });
        y = AddMenuItem(y, GlyphLog,      "View Log…", ShowLog);

        // Extra gap before Quit — makes it harder to accidentally hit
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

        var logoPath = FluentTheme.FindAsset("logosmall.png");
        if (logoPath != null)
            panel.Controls.Add(new PictureBox
            {
                Image     = Image.FromFile(logoPath),
                SizeMode  = PictureBoxSizeMode.Zoom,
                Size      = new Size(32, 32),
                Location  = new Point(14, 18),
                BackColor = Color.Transparent,
            });

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

    // ── Now Playing ───────────────────────────────────────────────────────────

    private int AddNowPlaying(int y, string? track, string? artist, bool isLoved)
    {
        bool hasTrack = !string.IsNullOrWhiteSpace(track);
        int  h        = hasTrack ? 62 : 42;
        var  panelBg  = FluentTheme.IsDarkMode() ? Color.FromArgb(46, 46, 46) : Color.FromArgb(237, 237, 237);

        var panel = new Panel { BackColor = panelBg, Location = new Point(0, y), Size = new Size(W, h) };

        // Music note glyph
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

            // ── Heart / love button ───────────────────────────────────────────
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

        var isDark = FluentTheme.IsDarkMode();
        var bg     = isDark ? Color.FromArgb(18, 18, 18) : Color.FromArgb(248, 248, 248);
        var fg     = isDark ? Color.FromArgb(212, 212, 212) : Color.FromArgb(24, 24, 24);
        var hdrBg  = isDark ? Color.FromArgb(28, 28, 28) : Color.FromArgb(238, 238, 238);

        using var dlg = new Form
        {
            Text          = "WinScrobb — Activity Log",
            Size          = new Size(680, 480),
            MinimumSize   = new Size(480, 320),
            StartPosition = FormStartPosition.CenterScreen,
            BackColor     = bg,
            ForeColor     = fg,
            Font          = FluentTheme.Body(),
        };
        dlg.Load += (_, _) => FluentTheme.ApplyChrome(dlg);
        var icoPath = FluentTheme.FindAsset("icon.ico");
        if (icoPath != null) try { dlg.Icon = new Icon(icoPath); } catch { }

        // ── Header bar ────────────────────────────────────────────────────────
        var header = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = hdrBg };

        var logoPath = FluentTheme.FindAsset("logosmall.png");
        if (logoPath != null)
            header.Controls.Add(new PictureBox
            {
                Image     = Image.FromFile(logoPath),
                SizeMode  = PictureBoxSizeMode.Zoom,
                Size      = new Size(22, 22),
                Location  = new Point(14, 14),
                BackColor = Color.Transparent,
            });

        header.Controls.Add(new Label
        {
            Text      = "Activity Log",
            Font      = FluentTheme.Subtitle(11f),
            ForeColor = fg,
            AutoSize  = true,
            Location  = new Point(logoPath != null ? 44 : 14, 15),
            BackColor = Color.Transparent,
        });

        // Entry count, right-aligned, updates when layout runs
        var countLbl = new Label
        {
            Text      = $"{LogEntries.Count} entries",
            Font      = FluentTheme.Caption(8f),
            ForeColor = FluentTheme.TextMuted,
            AutoSize  = true,
            BackColor = Color.Transparent,
        };
        header.Controls.Add(countLbl);
        header.Layout += (_, _) =>
            countLbl.Location = new Point(header.Width - countLbl.Width - 14,
                                          (header.Height - countLbl.Height) / 2);

        dlg.Controls.Add(header);

        // Divider below header
        dlg.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = FluentTheme.Divider });

        // ── Log text area ─────────────────────────────────────────────────────
        var rawText = LogEntries.Count == 0
            ? "  (no log entries yet)"
            : string.Join(Environment.NewLine, LogEntries);

        var box = new RichTextBox
        {
            Dock        = DockStyle.Fill,
            ReadOnly    = true,
            BorderStyle = BorderStyle.None,
            BackColor   = bg,
            ForeColor   = fg,
            Font        = new Font("Cascadia Mono", 9f),
            Text        = rawText,
            Padding     = new Padding(10),
            ScrollBars  = RichTextBoxScrollBars.Vertical,
            WordWrap    = false,
        };

        dlg.Controls.Add(box);

        // Colour [HH:mm:ss] timestamps in accent colour, then scroll to bottom
        dlg.Shown += (_, _) =>
        {
            ColourTimestamps(box, FluentTheme.Accent);
            box.SelectionStart = box.Text.Length;
            box.ScrollToCaret();
        };

        dlg.ShowDialog();
        _childOpen = false;
    }

    /// <summary>Highlights [timestamp] tokens in accent colour inside a RichTextBox.</summary>
    private static void ColourTimestamps(RichTextBox box, Color accent)
    {
        var full = box.Text;
        int i = 0;
        while (i < full.Length)
        {
            int open  = full.IndexOf('[', i);
            if (open < 0) break;
            int close = full.IndexOf(']', open);
            if (close < 0) break;

            box.Select(open, close - open + 1);
            box.SelectionColor = accent;
            i = close + 1;
        }
        // Reset selection so keyboard/scroll works normally
        box.Select(0, 0);
        box.SelectionColor = box.ForeColor;
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

    // Only auto-close on deactivate when no child dialog is open
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

    public static TrayPopup Create(string username, string? nowPlaying, string? nowPlayingArtist, bool isLoved = false)
        => new(username, nowPlaying, nowPlayingArtist, isLoved);

    public void ShowNearCursor()
    {
        var cursor = Cursor.Position;
        var screen = Screen.FromPoint(cursor);
        int x = Math.Max(screen.WorkingArea.Left + 8,
            Math.Min(cursor.X - Width / 2, screen.WorkingArea.Right - Width - 8));
        // 20px above taskbar — Quit stays comfortably away from the click target
        int y = screen.WorkingArea.Bottom - Height - 20;
        Location = new Point(x, y);
        Show();
        Activate();
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
