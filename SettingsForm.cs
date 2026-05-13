namespace WinScrobb;

public class SettingsForm : Form
{
    private readonly FluentInput _apiKey = new();
    private readonly FluentInput _apiSecret = new() { IsPassword = true };
    private readonly FluentButton _saveBtn = new() { IsAccent = true };
    private readonly FluentButton _cancelBtn = new() { Text = "Cancel" };
    private readonly Label _statusLbl = new();

    private readonly FluentToggle _startupToggle = new();
    private readonly FluentToggle _ipodEnableToggle = new();
    private readonly FluentToggle _ipodAutoSyncToggle = new();
    private readonly FluentToggle _retroIconToggle = new();

    private Panel? _mainHost;
    private FlowLayoutPanel? _content;

    public AppConfig Config { get; }
    private readonly string _origApiKey;
    private readonly string _origApiSecret;

    private const int MinW = 760;
    private const int MinH = 620;
    private const int SideW = 184;
    private const int Pad = 24;

    public SettingsForm(AppConfig existing)
    {
        Config = existing;
        _origApiKey = existing.ApiKey ?? "";
        _origApiSecret = existing.ApiSecret ?? "";
        Build();
    }

    private void Build()
    {
        SuspendLayout();

        Text = "WinScrobb Settings";
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(MinW, MinH);
        MinimumSize = new Size(680, 540);
        BackColor = FluentTheme.Surface;
        ForeColor = FluentTheme.TextPrimary;
        Font = FluentTheme.Body();
        SetIcon();

        var sidebar = BuildSidebar();
        Controls.Add(sidebar);
        Controls.Add(new Panel { Dock = DockStyle.Left, Width = 1, BackColor = FluentTheme.Divider });

        var footer = BuildFooter();
        Controls.Add(footer);
        Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = FluentTheme.Divider });

        _mainHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FluentTheme.Surface,
        };
        Controls.Add(_mainHost);

        _content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(Pad, 20, Pad, 20),
            BackColor = FluentTheme.Surface,
        };
        _mainHost.Controls.Add(_content);

        _content.Controls.Add(BuildHero());
        _content.Controls.Add(BuildAccountPanel());
        _content.Controls.Add(BuildBehaviorPanel());
        _content.Controls.Add(BuildIPodPanel());
        if (Config.RetroIconUnlocked)
            _content.Controls.Add(BuildPersonalizationPanel());

        _content.SizeChanged += (_, _) => ResizeContentCards();
        ResizeContentCards();

        _apiKey.ValueChanged += (_, _) => UpdateSaveBtnText();
        _apiSecret.ValueChanged += (_, _) => UpdateSaveBtnText();
        UpdateSaveBtnText();

        ResumeLayout(false);
    }

    private Panel BuildFooter()
    {
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 74, BackColor = FluentTheme.Surface };

        _statusLbl.Font = FluentTheme.Caption(8.5f);
        _statusLbl.ForeColor = FluentTheme.TextMuted;
        _statusLbl.AutoSize = false;
        _statusLbl.BackColor = FluentTheme.Surface;
        footer.Controls.Add(_statusLbl);

        _cancelBtn.Size = new Size(104, 34);
        _saveBtn.Size = new Size(150, 34);
        _cancelBtn.Font = FluentTheme.Body(9.5f);
        _saveBtn.Font = FluentTheme.Body(9.5f);
        _cancelBtn.Click += (_, _) => DialogResult = DialogResult.Cancel;
        _saveBtn.Click += OnSave;
        footer.Controls.Add(_cancelBtn);
        footer.Controls.Add(_saveBtn);

        footer.Layout += (_, _) =>
        {
            _saveBtn.Location = new Point(footer.Width - Pad - _saveBtn.Width, 20);
            _cancelBtn.Location = new Point(_saveBtn.Left - _cancelBtn.Width - 8, 20);
            _statusLbl.Location = new Point(Pad, 28);
            _statusLbl.Size = new Size(Math.Max(60, _cancelBtn.Left - Pad - 16), 20);
        };

        return footer;
    }

    private Panel BuildSidebar()
    {
        var sidebar = new Panel { Dock = DockStyle.Left, Width = SideW, BackColor = FluentTheme.Surface };

        var logoPath = FluentTheme.FindAsset("logosmall.png");
        if (logoPath != null)
            sidebar.Controls.Add(new PictureBox
            {
                Image = Image.FromFile(logoPath),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(38, 38),
                Location = new Point(22, 24),
                BackColor = Color.Transparent,
            });

        sidebar.Controls.Add(new Label
        {
            Text = "WinScrobb",
            Font = FluentTheme.Subtitle(12.5f),
            ForeColor = FluentTheme.TextPrimary,
            AutoSize = true,
            Location = new Point(68, 26),
            BackColor = Color.Transparent,
        });

        var byline = new LinkLabel
        {
            Text = "an app by h3",
            Font = FluentTheme.Caption(8.5f),
            LinkColor = FluentTheme.Accent,
            ActiveLinkColor = FluentTheme.AccentPress,
            VisitedLinkColor = FluentTheme.Accent,
            AutoSize = true,
            Location = new Point(70, 48),
            BackColor = Color.Transparent,
        };
        byline.LinkClicked += (_, _) => System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("https://h3nry.xyz") { UseShellExecute = true });
        sidebar.Controls.Add(byline);

        var navTop = 104;
        sidebar.Controls.Add(NavItem("Account", "\uE77B", navTop, true, "account"));
        sidebar.Controls.Add(NavItem("Behavior", "\uE713", navTop + 42, false, "behavior"));
        sidebar.Controls.Add(NavItem("iPod sync", "\uE8E5", navTop + 84, false, "ipod"));
        if (Config.RetroIconUnlocked)
            sidebar.Controls.Add(NavItem("Style", "\uE771", navTop + 126, false, "style"));

        return sidebar;
    }

    private Control NavItem(string text, string glyph, int top, bool selected, string targetTag)
    {
        var item = new Panel
        {
            Size = new Size(SideW - 22, 34),
            Location = new Point(11, top),
            Cursor = Cursors.Hand,
            BackColor = selected
                ? (FluentTheme.IsDarkMode() ? Color.FromArgb(47, 47, 47) : Color.FromArgb(235, 243, 252))
                : FluentTheme.Surface,
        };
        item.Click += (_, _) => ScrollToSection(targetTag);

        var icon = new Label
        {
            Text = glyph,
            Font = new Font("Segoe MDL2 Assets", 10f),
            ForeColor = selected ? FluentTheme.Accent : FluentTheme.TextMuted,
            AutoSize = false,
            Size = new Size(30, 34),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent,
        };
        icon.Click += (_, _) => ScrollToSection(targetTag);
        item.Controls.Add(icon);

        var label = new Label
        {
            Text = text,
            Font = FluentTheme.Body(9.5f),
            ForeColor = selected ? FluentTheme.TextPrimary : FluentTheme.TextMuted,
            AutoSize = false,
            Location = new Point(36, 0),
            Size = new Size(item.Width - 42, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent,
        };
        label.Click += (_, _) => ScrollToSection(targetTag);
        item.Controls.Add(label);
        return item;
    }

    private void ScrollToSection(string targetTag)
    {
        if (_content is null) return;
        foreach (Control control in _content.Controls)
        {
            if (Equals(control.Tag, targetTag))
            {
                _content.ScrollControlIntoView(control);
                break;
            }
        }
    }

    private Control BuildHero()
    {
        var hero = new Panel { Height = 72, Margin = new Padding(0, 0, 0, 10), BackColor = FluentTheme.Surface };
        hero.Controls.Add(new Label
        {
            Text = "Settings",
            Font = FluentTheme.Display(21f),
            ForeColor = FluentTheme.TextPrimary,
            AutoSize = true,
            Location = new Point(0, 3),
            BackColor = Color.Transparent,
        });
        hero.Controls.Add(new Label
        {
            Text = Config.IsAuthenticated
                ? $"Signed in as {Config.Username}"
                : "Connect Last.fm, sync your iPod, and keep the tray app tidy.",
            Font = FluentTheme.Body(9.5f),
            ForeColor = FluentTheme.TextMuted,
            AutoSize = true,
            Location = new Point(2, 42),
            BackColor = Color.Transparent,
        });
        return hero;
    }

    private Control BuildAccountPanel()
    {
        var card = Card(208);
        card.Tag = "account";
        card.Controls.Add(PanelTitle("Last.fm account", "Only re-authorizes when your key or secret changes.", 20, 16));

        _apiKey.Value = Config.ApiKey;
        _apiKey.Font = FluentTheme.Body(9.5f);
        _apiKey.Location = new Point(20, 78);
        card.Controls.Add(FieldLabel("API key", 20, 56));
        card.Controls.Add(_apiKey);

        _apiSecret.Value = Config.ApiSecret;
        _apiSecret.Font = FluentTheme.Body(9.5f);
        _apiSecret.Location = new Point(20, 142);
        card.Controls.Add(FieldLabel("API secret", 20, 120));
        card.Controls.Add(_apiSecret);

        var link = new LinkLabel
        {
            Text = "Create a Last.fm API account",
            Font = FluentTheme.Caption(),
            LinkColor = FluentTheme.Accent,
            ActiveLinkColor = FluentTheme.AccentPress,
            AutoSize = true,
            BackColor = FluentTheme.Card,
            Location = new Point(20, 184),
        };
        link.LinkClicked += (_, _) => System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("https://www.last.fm/api/account/create") { UseShellExecute = true });
        card.Controls.Add(link);

        return card;
    }

    private Control BuildBehaviorPanel()
    {
        var card = Card(104);
        card.Tag = "behavior";
        card.Controls.Add(PanelTitle("Behavior", "Choose how WinScrobb behaves in Windows.", 20, 16));
        _startupToggle.Checked = Config.RunAtStartup;
        card.Controls.Add(SettingRow("Launch at sign-in", "Start WinScrobb when Windows starts.", _startupToggle, 20, 62));
        return card;
    }

    private Control BuildIPodPanel()
    {
        var card = Card(190);
        card.Tag = "ipod";
        var connected = IPodDetector.FindConnectedIPods();
        var status = connected.Count == 0
            ? "No iPod connected."
            : $"Connected: {connected[0].Name} ({(connected[0].IsCompressed ? "iTunesCDB" : "iTunesDB")})";

        card.Controls.Add(PanelTitle("iPod sync", status, 20, 16));

        _ipodEnableToggle.Checked = Config.IPodSyncEnabled;
        _ipodAutoSyncToggle.Checked = Config.IPodAutoSyncOnConnect;
        _ipodEnableToggle.CheckedChanged += (_, _) => RefreshIPodAutoSync();

        card.Controls.Add(SettingRow("Enable iPod sync", "Read iPod play counts and submit new plays to Last.fm.", _ipodEnableToggle, 20, 70));
        card.Controls.Add(SettingRow("Auto-sync on connect", "Scrobble automatically when new plays are detected.", _ipodAutoSyncToggle, 20, 120));
        RefreshIPodAutoSync();

        return card;
    }

    private Control BuildPersonalizationPanel()
    {
        var card = Card(104);
        card.Tag = "style";
        card.Controls.Add(PanelTitle("Personalization", "Small victories deserve tiny style switches.", 20, 16));
        _retroIconToggle.Checked = Config.UseRetroIcon;
        card.Controls.Add(SettingRow("Use retro tray icon", "Unlocked from the logo click ritual.", _retroIconToggle, 20, 62));
        return card;
    }

    private FluentCard Card(int height) => new()
    {
        Width = 520,
        Height = height,
        Margin = new Padding(0, 0, 0, 14),
        BackColor = FluentTheme.Card,
    };

    private Control PanelTitle(string title, string subtitle, int x, int y)
    {
        var panel = new Panel { Location = new Point(x, y), Size = new Size(460, 36), BackColor = FluentTheme.Card };
        panel.Controls.Add(new Label
        {
            Text = title,
            Font = FluentTheme.Subtitle(11.5f),
            ForeColor = FluentTheme.TextPrimary,
            AutoSize = true,
            BackColor = FluentTheme.Card,
        });
        panel.Controls.Add(new Label
        {
            Text = subtitle,
            Font = FluentTheme.Caption(8.5f),
            ForeColor = FluentTheme.TextMuted,
            AutoSize = true,
            Location = new Point(0, 21),
            BackColor = FluentTheme.Card,
        });
        return panel;
    }

    private static Label FieldLabel(string text, int x, int y) => new()
    {
        Text = text,
        Font = FluentTheme.Caption(8.5f),
        ForeColor = FluentTheme.TextMuted,
        AutoSize = true,
        Location = new Point(x, y),
        BackColor = FluentTheme.Card,
    };

    private Control SettingRow(string title, string subtitle, FluentToggle toggle, int x, int y)
    {
        var row = new Panel { Location = new Point(x, y), Size = new Size(460, 42), BackColor = FluentTheme.Card };
        row.Controls.Add(new Label
        {
            Text = title,
            Font = FluentTheme.Body(9.5f),
            ForeColor = FluentTheme.TextPrimary,
            AutoSize = true,
            Location = new Point(0, 1),
            BackColor = FluentTheme.Card,
        });
        row.Controls.Add(new Label
        {
            Text = subtitle,
            Font = FluentTheme.Caption(8.3f),
            ForeColor = FluentTheme.TextMuted,
            AutoSize = true,
            Location = new Point(0, 22),
            BackColor = FluentTheme.Card,
        });
        toggle.Location = new Point(row.Width - toggle.Width, 10);
        row.Controls.Add(toggle);
        row.Resize += (_, _) => toggle.Location = new Point(row.Width - toggle.Width, 10);
        return row;
    }

    private void ResizeContentCards()
    {
        if (_content is null) return;
        var width = Math.Max(380, _content.ClientSize.Width - _content.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 12);
        foreach (Control control in _content.Controls)
        {
            control.Width = width;
            foreach (Control child in control.Controls)
            {
                if (child is FluentInput input)
                    input.Width = width - 40;
                else if (child is Panel panel && panel.Width >= 440)
                    panel.Width = width - 40;
            }
        }
    }

    private void RefreshIPodAutoSync()
    {
        _ipodAutoSyncToggle.Enabled = _ipodEnableToggle.Checked;
        _ipodAutoSyncToggle.Cursor = _ipodAutoSyncToggle.Enabled ? Cursors.Hand : Cursors.Default;
        _ipodAutoSyncToggle.Invalidate();
    }

    private void UpdateSaveBtnText()
    {
        bool needsAuth =
            _apiKey.Value.Trim() != _origApiKey ||
            _apiSecret.Value.Trim() != _origApiSecret ||
            string.IsNullOrEmpty(Config.SessionKey);

        _saveBtn.Text = needsAuth ? "Save and authorize" : "Save";
    }

    private void SetIcon()
    {
        var icoPath = FluentTheme.FindAsset("icon.ico");
        if (icoPath != null) try { Icon = new Icon(icoPath); } catch { }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        FluentTheme.ApplyChrome(this);
    }

    private async void OnSave(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_apiKey.Value) || string.IsNullOrWhiteSpace(_apiSecret.Value))
        {
            MessageBox.Show("Both API key and API secret are required.", "WinScrobb",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var newKey = _apiKey.Value.Trim();
        var newSecret = _apiSecret.Value.Trim();
        bool credsUnchanged =
            newKey == _origApiKey &&
            newSecret == _origApiSecret &&
            !string.IsNullOrEmpty(Config.SessionKey);

        if (credsUnchanged)
        {
            SaveLocalOptions();
            DialogResult = DialogResult.OK;
            return;
        }

        _saveBtn.Enabled = false;
        Status("Requesting auth token...");

        Config.ApiKey = newKey;
        Config.ApiSecret = newSecret;
        Config.SessionKey = "";
        Config.Username = "";

        using var client = new LastFmClient(Config.ApiKey, Config.ApiSecret);
        try
        {
            var token = await client.GetTokenAsync();
            var url = LastFmClient.AuthUrl(Config.ApiKey, token);

            Status("Browser opened. Authorize WinScrobb, then continue.");
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });

            var result = MessageBox.Show(
                "After you've authorized WinScrobb on Last.fm, click Continue.",
                "WinScrobb Authorization",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Information);

            if (result != DialogResult.OK)
            {
                _saveBtn.Enabled = true;
                Status("");
                return;
            }

            Status("Completing sign-in...");
            var (sessionKey, username) = await client.GetSessionAsync(token);
            Config.SessionKey = sessionKey;
            Config.Username = username;
            SaveLocalOptions();
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            _saveBtn.Enabled = true;
            Status($"Error: {ex.Message}");
        }
    }

    private void SaveLocalOptions()
    {
        Config.RunAtStartup = _startupToggle.Checked;
        Config.IPodSyncEnabled = _ipodEnableToggle.Checked;
        Config.IPodAutoSyncOnConnect = _ipodEnableToggle.Checked && _ipodAutoSyncToggle.Checked;
        Config.UseRetroIcon = Config.RetroIconUnlocked && _retroIconToggle.Checked;
        Config.Save();
        Config.ApplyStartup();
    }

    private void Status(string msg) => _statusLbl.Text = msg;

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Enter) { OnSave(null, EventArgs.Empty); return true; }
        if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
