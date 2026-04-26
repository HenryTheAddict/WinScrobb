using System.Runtime.InteropServices;

namespace WinScrobb;

/// <summary>
/// Dedicated iPod menu — shown when the user clicks the iPod banner in the
/// tray popup. Surfaces device info, recent activity, and the sync action.
/// </summary>
public sealed class IPodForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public event EventHandler? SyncRequested;
    public event EventHandler<bool>? AutoSyncToggled;

    private readonly IPodDeviceInfo _device;
    private readonly AppConfig      _config;
    private readonly IReadOnlyList<string> _log;

    private FluentButton? _syncBtn;
    private Label?        _statusLbl;
    private Label?        _newPlaysLbl;

    public IPodForm(IPodDeviceInfo device, AppConfig config, IReadOnlyList<string> log)
    {
        _device = device;
        _config = config;
        _log    = log;

        Text          = $"WinScrobb — {device.Name}";
        Size          = new Size(560, 540);
        MinimumSize   = new Size(420, 380);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor     = FluentTheme.Surface;
        ForeColor     = FluentTheme.TextPrimary;
        Font          = FluentTheme.Body();

        var icoPath = FluentTheme.FindAsset("icon.ico");
        if (icoPath != null) try { Icon = new Icon(icoPath); } catch { }

        Build();
    }

    private void Build()
    {
        // ── Header ───────────────────────────────────────────────────────────
        var hdrBg = FluentTheme.IsDarkMode() ? Color.FromArgb(28, 28, 32) : Color.FromArgb(238, 238, 241);
        var header = new Panel { Dock = DockStyle.Top, Height = 86, BackColor = hdrBg };

        var icon = FluentTheme.FindAsset("logosmall.png");
        if (icon != null)
            header.Controls.Add(new PictureBox
            {
                Image     = Image.FromFile(icon),
                SizeMode  = PictureBoxSizeMode.Zoom,
                Size      = new Size(48, 48),
                Location  = new Point(20, 20),
                BackColor = Color.Transparent,
            });

        header.Controls.Add(new Label
        {
            Text      = _device.Name,
            Font      = FluentTheme.Display(15f),
            ForeColor = FluentTheme.TextPrimary,
            AutoSize  = true,
            Location  = new Point(80, 20),
            BackColor = Color.Transparent,
        });

        var subText = _device.IsCompressed
            ? $"{_device.MountPath}  •  iTunesCDB (compressed)"
            : $"{_device.MountPath}  •  iTunesDB";
        header.Controls.Add(new Label
        {
            Text      = subText,
            Font      = FluentTheme.Caption(8.5f),
            ForeColor = FluentTheme.TextMuted,
            AutoSize  = true,
            Location  = new Point(80, 48),
            BackColor = Color.Transparent,
        });

        Controls.Add(header);
        Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = FluentTheme.Divider });

        // ── Stats row ────────────────────────────────────────────────────────
        var stats = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = FluentTheme.Surface };

        int newPlays = IPodSyncEngine.CountNewPlays(_device, _config);
        var lastSync = _config.GetLastIPodSync(_device.Id);

        AddStat(stats, 24,  16, "New plays", newPlays.ToString());
        _newPlaysLbl = (Label)stats.Controls[^1]; // remember the value label so we can refresh

        AddStat(stats, 200, 16, "Last sync",
            lastSync == DateTime.MinValue ? "never" : lastSync.ToLocalTime().ToString("MMM d, HH:mm"));

        AddStat(stats, 380, 16, "Library",
            _device.IsCompressed ? "iTunesCDB" : "iTunesDB");

        Controls.Add(stats);
        Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = FluentTheme.Divider });

        // ── Options + Sync row ───────────────────────────────────────────────
        var opts = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = FluentTheme.Surface };

        var autoSync = new CheckBox
        {
            Text      = "Auto-sync when this iPod is connected",
            Font      = FluentTheme.Body(9.5f),
            ForeColor = FluentTheme.TextPrimary,
            BackColor = FluentTheme.Surface,
            Checked   = _config.IPodAutoSyncOnConnect,
            AutoSize  = true,
            Location  = new Point(20, 22),
        };
        autoSync.CheckedChanged += (_, _) =>
        {
            _config.IPodAutoSyncOnConnect = autoSync.Checked;
            _config.Save();
            AutoSyncToggled?.Invoke(this, autoSync.Checked);
        };
        opts.Controls.Add(autoSync);

        _syncBtn = new FluentButton
        {
            Text     = newPlays > 0 ? $"Sync now ({newPlays})" : "Sync now",
            IsAccent = true,
            Size     = new Size(170, 36),
        };
        _syncBtn.Click += (_, _) => SyncRequested?.Invoke(this, EventArgs.Empty);
        opts.Controls.Add(_syncBtn);
        opts.Layout += (_, _) =>
            _syncBtn.Location = new Point(opts.Width - _syncBtn.Width - 20, (opts.Height - _syncBtn.Height) / 2);

        Controls.Add(opts);
        Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = FluentTheme.Divider });

        // ── Recent iPod activity (filtered log) ──────────────────────────────
        var sectLbl = new Label
        {
            Text      = "Recent iPod activity",
            Font      = FluentTheme.Body(9.5f),
            ForeColor = FluentTheme.TextMuted,
            AutoSize  = true,
            Location  = new Point(20, 12),
            BackColor = FluentTheme.Surface,
        };
        var sectPanel = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = FluentTheme.Surface };
        sectPanel.Controls.Add(sectLbl);
        Controls.Add(sectPanel);

        var bodyBg = FluentTheme.IsDarkMode() ? Color.FromArgb(20, 20, 22) : Color.FromArgb(248, 248, 250);
        var box = new RichTextBox
        {
            Dock        = DockStyle.Fill,
            ReadOnly    = true,
            BorderStyle = BorderStyle.None,
            BackColor   = bodyBg,
            ForeColor   = FluentTheme.TextPrimary,
            Font        = new Font("Cascadia Mono", 9f),
            ScrollBars  = RichTextBoxScrollBars.Vertical,
            WordWrap    = false,
        };
        Controls.Add(box);

        // Status bar
        _statusLbl = new Label
        {
            Dock      = DockStyle.Bottom,
            Height    = 22,
            Text      = "",
            Font      = FluentTheme.Caption(8f),
            ForeColor = FluentTheme.TextMuted,
            BackColor = hdrBg,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(20, 0, 20, 0),
        };
        Controls.Add(_statusLbl);

        Shown += (_, _) => RenderLog(box);
    }

    private static void AddStat(Panel host, int x, int y, string label, string value)
    {
        host.Controls.Add(new Label
        {
            Text      = label.ToUpperInvariant(),
            Font      = FluentTheme.Caption(7.5f),
            ForeColor = FluentTheme.TextMuted,
            AutoSize  = true,
            Location  = new Point(x, y),
            BackColor = FluentTheme.Surface,
        });
        host.Controls.Add(new Label
        {
            Text      = value,
            Font      = FluentTheme.Display(16f),
            ForeColor = FluentTheme.TextPrimary,
            AutoSize  = true,
            Location  = new Point(x, y + 18),
            BackColor = FluentTheme.Surface,
        });
    }

    private void RenderLog(RichTextBox box)
    {
        var lines = _log
            .Where(l => l.Contains("iPod", StringComparison.OrdinalIgnoreCase) ||
                        l.Contains("iTunes",  StringComparison.OrdinalIgnoreCase) ||
                        l.Contains("scrobbled", StringComparison.OrdinalIgnoreCase))
            .TakeLast(200)
            .ToList();

        box.Clear();
        if (lines.Count == 0)
        {
            box.SelectionColor = FluentTheme.TextMuted;
            box.AppendText("  Connect or sync the iPod to see activity here.");
            return;
        }

        foreach (var line in lines)
        {
            int b = line.IndexOf(']');
            if (line.StartsWith("[") && b > 0)
            {
                box.SelectionColor = FluentTheme.Accent;
                box.AppendText(line[..(b + 1)]);
                box.SelectionColor = FluentTheme.TextPrimary;
                box.AppendText(line[(b + 1)..]);
            }
            else box.AppendText(line);
            box.AppendText("\n");
        }
        box.SelectionStart = box.Text.Length;
        box.ScrollToCaret();
    }

    public void SetStatus(string text)
    {
        if (_statusLbl is null) return;
        if (InvokeRequired) { Invoke(() => SetStatus(text)); return; }
        _statusLbl.Text = text;
    }

    public void SetSyncing(bool syncing, int? newPlays = null)
    {
        if (_syncBtn is null) return;
        if (InvokeRequired) { Invoke(() => SetSyncing(syncing, newPlays)); return; }
        _syncBtn.Enabled = !syncing;
        _syncBtn.Text    = syncing
            ? "Syncing…"
            : (newPlays > 0 ? $"Sync now ({newPlays})" : "Sync now");
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        FluentTheme.ApplyChrome(this);
        int dark = FluentTheme.IsDarkMode() ? 1 : 0;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
    }
}
