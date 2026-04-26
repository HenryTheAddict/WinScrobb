using System.Drawing.Drawing2D;

namespace WinScrobb;

public class SettingsForm : Form
{
    // ── Controls ──────────────────────────────────────────────────────────────
    private readonly FluentInput  _apiKey    = new();
    private readonly FluentInput  _apiSecret = new() { IsPassword = true };
    private readonly FluentButton _saveBtn   = new() { Text = "Save && Authorize", IsAccent = true };
    private readonly FluentButton _cancelBtn = new() { Text = "Cancel" };
    private readonly Label        _statusLbl = new();
    private readonly CheckBox     _startupCb = new();
    private readonly CheckBox     _ipodEnableCb   = new();
    private readonly CheckBox     _ipodAutoSyncCb = new();
    private readonly Label        _ipodStatusLbl  = new();
    private CheckBox?             _retroIconCb;

    public AppConfig Config { get; }
    private readonly string _origApiKey;
    private readonly string _origApiSecret;

    public SettingsForm(AppConfig existing)
    {
        Config         = existing;
        _origApiKey    = existing.ApiKey ?? "";
        _origApiSecret = existing.ApiSecret ?? "";
        Build();
    }

    // ── Layout constants ──────────────────────────────────────────────────────

    private const int W      = 500;   // client width
    private const int Pad    = 24;    // outer horizontal padding
    private const int CardW  = W - Pad * 2;

    // ── Build ─────────────────────────────────────────────────────────────────

    private void Build()
    {
        SuspendLayout();

        Text            = "WinScrobb — Settings";
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        ClientSize      = new Size(W, 620);
        MinimumSize     = new Size(W, 460);
        BackColor       = FluentTheme.Surface;
        ForeColor       = FluentTheme.TextPrimary;
        Font            = FluentTheme.Body();

        SetIcon();

        // ── Footer (status + buttons), sticky at bottom ───────────────────────
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 88, BackColor = FluentTheme.Surface };

        _cancelBtn.Size = new Size(110, 34);
        _saveBtn.Size   = new Size(152, 34);
        _cancelBtn.Font = FluentTheme.Body(9.5f);
        _saveBtn.Font   = FluentTheme.Body(9.5f);
        _cancelBtn.Click += (_, _) => DialogResult = DialogResult.Cancel;
        _saveBtn.Click   += OnSave;
        footer.Controls.Add(_cancelBtn);
        footer.Controls.Add(_saveBtn);

        _statusLbl.Font      = FluentTheme.Caption(8.5f);
        _statusLbl.ForeColor = FluentTheme.TextMuted;
        _statusLbl.AutoSize  = false;
        _statusLbl.Size      = new Size(W - Pad * 2, 16);
        _statusLbl.BackColor = FluentTheme.Surface;
        footer.Controls.Add(_statusLbl);

        footer.Layout += (_, _) =>
        {
            _cancelBtn.Location = new Point(footer.Width - Pad - _cancelBtn.Width - _saveBtn.Width - 8, 22);
            _saveBtn.Location   = new Point(footer.Width - Pad - _saveBtn.Width, 22);
            _statusLbl.Location = new Point(Pad, footer.Height - 28);
            _statusLbl.Width    = footer.Width - Pad * 2;
        };

        Controls.Add(footer);
        Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = FluentTheme.Divider });

        // ── Header (logo + title), sticky at top ──────────────────────────────
        var header = new Panel { Dock = DockStyle.Top, Height = 86, BackColor = FluentTheme.Surface };
        BuildHeader(header);
        Controls.Add(header);
        Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = FluentTheme.Divider });

        // ── Stack of collapsible sections (scrollable) ────────────────────────
        var stack = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents  = false,
            AutoScroll    = true,
            Padding       = new Padding(Pad, 8, Pad, 8),
            BackColor     = FluentTheme.Surface,
        };
        Controls.Add(stack);

        AddSection(stack, "Last.fm account",            BuildAccountCard(),     expanded: true);
        AddSection(stack, "Behavior",                   BuildBehaviorCard(),    expanded: true);
        AddSection(stack, "iPod sync (Classic / Nano)", BuildIPodCard(),        expanded: true);
        if (Config.RetroIconUnlocked)
            AddSection(stack, "Personalization",        BuildPersonalizeCard(), expanded: true);

        ResumeLayout(false);
    }

    private void BuildHeader(Panel header)
    {
        var logoPath = FluentTheme.FindAsset("logosmall.png");
        if (logoPath != null)
            header.Controls.Add(new PictureBox
            {
                Image     = Image.FromFile(logoPath),
                SizeMode  = PictureBoxSizeMode.Zoom,
                Size      = new Size(38, 38),
                Location  = new Point(Pad, 22),
                BackColor = Color.Transparent,
            });

        header.Controls.Add(new Label
        {
            Text      = "WinScrobb",
            Font      = FluentTheme.Display(18f),
            ForeColor = FluentTheme.TextPrimary,
            AutoSize  = true,
            Location  = new Point(logoPath != null ? 70 : Pad, 20),
            BackColor = Color.Transparent,
        });
        header.Controls.Add(new Label
        {
            Text      = "Settings",
            Font      = FluentTheme.Caption(8.5f),
            ForeColor = FluentTheme.TextMuted,
            AutoSize  = true,
            Location  = new Point(logoPath != null ? 71 : Pad + 1, 50),
            BackColor = Color.Transparent,
        });
    }

    private void AddSection(FlowLayoutPanel host, string title, Control content, bool expanded)
    {
        var section = new CollapsibleSection(title, expanded)
        {
            Width  = host.ClientSize.Width - host.Padding.Horizontal,
            Margin = new Padding(0, 4, 0, 12),
        };
        section.AddContent(content);

        // Keep section width matched to the FlowLayoutPanel
        host.SizeChanged += (_, _) =>
            section.Width = host.ClientSize.Width - host.Padding.Horizontal;

        host.Controls.Add(section);
    }

    // ── Account card (API key + secret + helper link) ─────────────────────────
    private Control BuildAccountCard()
    {
        var panel = new Panel { Width = CardW, Height = 220, BackColor = FluentTheme.Surface };

        int cy = 4;
        panel.Controls.Add(RowLabelOnSurface("API Key", 0, cy));
        cy += 20;
        _apiKey.Value    = Config.ApiKey;
        _apiKey.Font     = FluentTheme.Body(9.5f);
        _apiKey.Size     = new Size(CardW, 38);
        _apiKey.Location = new Point(0, cy);
        panel.Controls.Add(_apiKey);
        cy += 50;

        panel.Controls.Add(RowLabelOnSurface("API Secret", 0, cy));
        cy += 20;
        _apiSecret.Value    = Config.ApiSecret;
        _apiSecret.Font     = FluentTheme.Body(9.5f);
        _apiSecret.Size     = new Size(CardW, 38);
        _apiSecret.Location = new Point(0, cy);
        panel.Controls.Add(_apiSecret);
        cy += 48;

        var link = new LinkLabel
        {
            Text      = "Get your API key at last.fm/api/account/create  →",
            Font      = FluentTheme.Caption(),
            AutoSize  = true,
            Location  = new Point(0, cy),
            BackColor = FluentTheme.Surface,
            LinkColor = FluentTheme.Accent,
        };
        link.LinkClicked += (_, _) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "https://www.last.fm/api/account/create") { UseShellExecute = true });
        panel.Controls.Add(link);

        panel.Height = cy + 24;
        return panel;
    }

    // ── Behavior card (run at startup, etc.) ──────────────────────────────────
    private Control BuildBehaviorCard()
    {
        var panel = new Panel { Width = CardW, Height = 40, BackColor = FluentTheme.Surface };

        _startupCb.Text      = "Launch WinScrobb when Windows starts";
        _startupCb.Font      = FluentTheme.Body(9.5f);
        _startupCb.ForeColor = FluentTheme.TextPrimary;
        _startupCb.BackColor = FluentTheme.Surface;
        _startupCb.Checked   = Config.RunAtStartup;
        _startupCb.AutoSize  = true;
        _startupCb.Location  = new Point(0, 4);
        panel.Controls.Add(_startupCb);

        return panel;
    }

    // ── iPod card ─────────────────────────────────────────────────────────────
    private Control BuildIPodCard()
    {
        var panel = new Panel { Width = CardW, Height = 130, BackColor = FluentTheme.Surface };

        var connected = IPodDetector.FindConnectedIPods();
        _ipodStatusLbl.Text = connected.Count == 0
            ? "No iPod connected — connect one to see options here."
            : $"Connected: {connected[0].Name}"
              + (connected[0].IsCompressed ? "  (iTunesCDB)" : "");
        _ipodStatusLbl.Font      = FluentTheme.Caption(8.5f);
        _ipodStatusLbl.ForeColor = connected.Count == 0
            ? FluentTheme.TextMuted
            : FluentTheme.Accent;
        _ipodStatusLbl.AutoSize  = true;
        _ipodStatusLbl.Location  = new Point(0, 4);
        _ipodStatusLbl.BackColor = FluentTheme.Surface;
        panel.Controls.Add(_ipodStatusLbl);

        _ipodEnableCb.Text      = "Enable iPod sync";
        _ipodEnableCb.Font      = FluentTheme.Body(9.5f);
        _ipodEnableCb.ForeColor = FluentTheme.TextPrimary;
        _ipodEnableCb.BackColor = FluentTheme.Surface;
        _ipodEnableCb.Checked   = Config.IPodSyncEnabled;
        _ipodEnableCb.AutoSize  = true;
        _ipodEnableCb.Location  = new Point(0, 32);
        panel.Controls.Add(_ipodEnableCb);

        _ipodAutoSyncCb.Text      = "Automatically sync when iPod is connected";
        _ipodAutoSyncCb.Font      = FluentTheme.Body(9.5f);
        _ipodAutoSyncCb.ForeColor = FluentTheme.TextPrimary;
        _ipodAutoSyncCb.BackColor = FluentTheme.Surface;
        _ipodAutoSyncCb.Checked   = Config.IPodAutoSyncOnConnect;
        _ipodAutoSyncCb.AutoSize  = true;
        _ipodAutoSyncCb.Location  = new Point(0, 60);
        panel.Controls.Add(_ipodAutoSyncCb);

        panel.Controls.Add(new Label
        {
            Text      = "Reads new plays from iPod_Control/iTunes/Play Counts",
            Font      = FluentTheme.Caption(8f),
            ForeColor = FluentTheme.TextMuted,
            AutoSize  = true,
            Location  = new Point(0, 92),
            BackColor = FluentTheme.Surface,
        });

        return panel;
    }

    // ── Personalization card ──────────────────────────────────────────────────
    private Control BuildPersonalizeCard()
    {
        var panel = new Panel { Width = CardW, Height = 40, BackColor = FluentTheme.Surface };

        _retroIconCb = new CheckBox
        {
            Text      = "✦  Use retro tray icon  (unlocked!)",
            Font      = FluentTheme.Body(9.5f),
            ForeColor = FluentTheme.Accent,
            BackColor = FluentTheme.Surface,
            Checked   = Config.UseRetroIcon,
            AutoSize  = true,
            Location  = new Point(0, 4),
        };
        panel.Controls.Add(_retroIconCb);

        return panel;
    }

    private static Label RowLabelOnSurface(string text, int x, int y) => new()
    {
        Text      = text,
        Font      = FluentTheme.Caption(8.5f),
        ForeColor = FluentTheme.TextMuted,
        AutoSize  = true,
        Location  = new Point(x, y),
        BackColor = FluentTheme.Surface,
    };


    // ── Small helpers ─────────────────────────────────────────────────────────

    private static Label SectionLabel(string text, int x, int y) => new()
    {
        Text      = text,
        Font      = FluentTheme.Body(9.5f),
        ForeColor = FluentTheme.TextMuted,
        AutoSize  = true,
        Location  = new Point(x, y),
        BackColor = Color.Transparent,
    };

    private static Label RowLabel(string text, int x, int y) => new()
    {
        Text      = text,
        Font      = FluentTheme.Caption(8.5f),
        ForeColor = FluentTheme.TextMuted,
        AutoSize  = true,
        Location  = new Point(x, y),
        // BackColor must match card so it doesn't look odd
        BackColor = FluentTheme.Card,
    };

    private void SetIcon()
    {
        var icoPath = FluentTheme.FindAsset("icon.ico");
        if (icoPath != null) try { Icon = new Icon(icoPath); } catch { }
    }

    // ── DWM chrome ────────────────────────────────────────────────────────────

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        FluentTheme.ApplyChrome(this);
    }

    // ── Auth flow ─────────────────────────────────────────────────────────────

    private async void OnSave(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_apiKey.Value) || string.IsNullOrWhiteSpace(_apiSecret.Value))
        {
            MessageBox.Show("Both API Key and API Secret are required.", "WinScrobb",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // ── Fast path: API creds unchanged → just save the toggles, skip Last.fm auth ──
        var newKey    = _apiKey.Value.Trim();
        var newSecret = _apiSecret.Value.Trim();
        bool credsUnchanged =
            newKey    == _origApiKey &&
            newSecret == _origApiSecret &&
            !string.IsNullOrEmpty(Config.SessionKey);

        if (credsUnchanged)
        {
            Config.RunAtStartup          = _startupCb.Checked;
            Config.IPodSyncEnabled       = _ipodEnableCb.Checked;
            Config.IPodAutoSyncOnConnect = _ipodAutoSyncCb.Checked;
            Config.UseRetroIcon          = _retroIconCb is { Checked: true };
            Config.Save();
            Config.ApplyStartup();
            DialogResult = DialogResult.OK;
            return;
        }

        _saveBtn.Enabled = false;
        Status("Requesting auth token…");

        Config.ApiKey     = newKey;
        Config.ApiSecret  = newSecret;
        Config.SessionKey = "";
        Config.Username   = "";

        using var client = new LastFmClient(Config.ApiKey, Config.ApiSecret);
        try
        {
            var token = await client.GetTokenAsync();
            var url   = LastFmClient.AuthUrl(Config.ApiKey, token);

            Status("Browser opened — authorize WinScrobb, then click Continue.");
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });

            var result = MessageBox.Show(
                "After you've authorized WinScrobb on Last.fm, click Continue.",
                "WinScrobb Authorization",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Information);

            if (result != DialogResult.OK) { _saveBtn.Enabled = true; Status(""); return; }

            Status("Completing sign-in…");
            var (sessionKey, username) = await client.GetSessionAsync(token);
            Config.SessionKey            = sessionKey;
            Config.Username              = username;
            Config.RunAtStartup          = _startupCb.Checked;
            Config.IPodSyncEnabled       = _ipodEnableCb.Checked;
            Config.IPodAutoSyncOnConnect = _ipodAutoSyncCb.Checked;
            Config.UseRetroIcon          = _retroIconCb is { Checked: true };
            Config.Save();
            Config.ApplyStartup();
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            _saveBtn.Enabled = true;
            Status($"Error: {ex.Message}");
        }
    }

    private void Status(string msg) => _statusLbl.Text = msg;

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Enter) { OnSave(null, EventArgs.Empty); return true; }
        if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
