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

    public AppConfig Config { get; }

    public SettingsForm(AppConfig existing)
    {
        Config = existing;
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
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        ClientSize      = new Size(W, 470);
        BackColor       = FluentTheme.Surface;
        ForeColor       = FluentTheme.TextPrimary;
        Font            = FluentTheme.Body();

        SetIcon();

        int y = 0;

        // ── Header ────────────────────────────────────────────────────────────
        y = AddHeader(y);

        // ── Rule ──────────────────────────────────────────────────────────────
        Controls.Add(new Panel { BackColor = FluentTheme.Divider, Left = 0, Top = y, Width = W, Height = 1 });
        y += 17;

        // ── Section label ─────────────────────────────────────────────────────
        Controls.Add(SectionLabel("Last.fm account", Pad, y));
        y += 28;

        // ── Card ──────────────────────────────────────────────────────────────
        y = AddCard(y);
        y += 16;

        // ── Link ──────────────────────────────────────────────────────────────
        var link = new LinkLabel
        {
            Text      = "Get your API key at last.fm/api/account/create  →",
            Font      = FluentTheme.Caption(),
            AutoSize  = true,
            Left      = Pad,
            Top       = y,
            BackColor = FluentTheme.Surface,
            LinkColor = FluentTheme.Accent,
        };
        link.LinkClicked += (_, _) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "https://www.last.fm/api/account/create") { UseShellExecute = true });
        Controls.Add(link);
        y += 26;

        // ── Startup checkbox ──────────────────────────────────────────────────
        _startupCb.Text      = "Launch WinScrobb when Windows starts";
        _startupCb.Font      = FluentTheme.Body(9.5f);
        _startupCb.ForeColor = FluentTheme.TextPrimary;
        _startupCb.BackColor = FluentTheme.Surface;
        _startupCb.Checked   = Config.RunAtStartup;
        _startupCb.AutoSize  = true;
        _startupCb.Location  = new Point(Pad, y);
        Controls.Add(_startupCb);
        y += 34;

        // ── Buttons ───────────────────────────────────────────────────────────
        _cancelBtn.Size     = new Size(110, 34);
        _saveBtn.Size       = new Size(152, 34);
        _cancelBtn.Location = new Point(W - Pad - 270, y);
        _saveBtn.Location   = new Point(W - Pad - 152, y);
        _cancelBtn.Font     = FluentTheme.Body(9.5f);
        _saveBtn.Font       = FluentTheme.Body(9.5f);
        _cancelBtn.Click   += (_, _) => DialogResult = DialogResult.Cancel;
        _saveBtn.Click     += OnSave;
        Controls.Add(_cancelBtn);
        Controls.Add(_saveBtn);
        y += 42;

        // ── Status ────────────────────────────────────────────────────────────
        _statusLbl.Font      = FluentTheme.Caption(8.5f);
        _statusLbl.ForeColor = FluentTheme.TextMuted;
        _statusLbl.AutoSize  = false;
        _statusLbl.Size      = new Size(W - Pad * 2, 16);
        _statusLbl.Location  = new Point(Pad, y);
        _statusLbl.BackColor = FluentTheme.Surface;
        Controls.Add(_statusLbl);
        y += 24;

        ClientSize = new Size(W, y);

        // FluentButton inherits Control, not Button, so AcceptButton/CancelButton
        // are wired via KeyDown instead (see OnKeyDown override below).
        ResumeLayout(false);
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private int AddHeader(int y)
    {
        var logoPath = FluentTheme.FindAsset("logosmall.png");
        if (logoPath != null)
        {
            Controls.Add(new PictureBox
            {
                Image     = Image.FromFile(logoPath),
                SizeMode  = PictureBoxSizeMode.Zoom,
                Size      = new Size(38, 38),
                Location  = new Point(Pad, 20),
                BackColor = Color.Transparent,
            });
        }

        Controls.Add(new Label
        {
            Text      = "WinScrobb",
            Font      = FluentTheme.Display(18f),
            ForeColor = FluentTheme.TextPrimary,
            AutoSize  = true,
            Location  = new Point(logoPath != null ? 70 : Pad, 18),
            BackColor = Color.Transparent,
        });

        Controls.Add(new Label
        {
            Text      = "Settings",
            Font      = FluentTheme.Caption(8.5f),
            ForeColor = FluentTheme.TextMuted,
            AutoSize  = true,
            Location  = new Point(logoPath != null ? 71 : Pad + 1, 48),
            BackColor = Color.Transparent,
        });

        return 74;
    }

    // ── Card ──────────────────────────────────────────────────────────────────

    private int AddCard(int startY)
    {
        // Measure rows
        const int rowH   = 74;  // label (18) + input (38) + gap (18)
        const int cardH  = 16 + rowH * 2 + 4;

        var card = new FluentCard
        {
            Location  = new Point(Pad, startY),
            Size      = new Size(CardW, cardH),
            BackColor = FluentTheme.Card,
        };

        int cy = 16;

        // API Key row
        card.Controls.Add(RowLabel("API Key", 16, cy));
        cy += 20;
        _apiKey.Value    = Config.ApiKey;
        _apiKey.Font     = FluentTheme.Body(9.5f);
        _apiKey.Size     = new Size(CardW - 32, 38);
        _apiKey.Location = new Point(16, cy);
        card.Controls.Add(_apiKey);
        cy += 46;

        // Thin divider between rows
        card.Controls.Add(new Panel { BackColor = FluentTheme.Divider, Left = 16, Top = cy, Width = CardW - 32, Height = 1 });
        cy += 12;

        // API Secret row
        card.Controls.Add(RowLabel("API Secret", 16, cy));
        cy += 20;
        _apiSecret.Value    = Config.ApiSecret;
        _apiSecret.Font     = FluentTheme.Body(9.5f);
        _apiSecret.Size     = new Size(CardW - 32, 38);
        _apiSecret.Location = new Point(16, cy);
        card.Controls.Add(_apiSecret);
        cy += 46;

        Controls.Add(card);
        return startY + cardH;
    }

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

        _saveBtn.Enabled = false;
        Status("Requesting auth token…");

        Config.ApiKey     = _apiKey.Value.Trim();
        Config.ApiSecret  = _apiSecret.Value.Trim();
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
            Config.SessionKey  = sessionKey;
            Config.Username    = username;
            Config.RunAtStartup = _startupCb.Checked;
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
