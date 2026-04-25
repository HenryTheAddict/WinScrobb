using System.Runtime.InteropServices;

namespace WinScrobb;

/// <summary>Compact progress dialog shown while downloading an update.</summary>
public sealed class UpdateProgressForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private readonly ProgressBar _bar;
    private readonly Label       _pctLbl;
    private readonly Label       _statusLbl;

    public UpdateProgressForm(UpdateInfo info)
    {
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox     = false;
        MinimizeBox     = false;
        ShowInTaskbar   = true;
        StartPosition   = FormStartPosition.CenterScreen;
        ClientSize      = new Size(400, 130);
        BackColor       = FluentTheme.Surface;
        ForeColor       = FluentTheme.TextPrimary;
        Font            = FluentTheme.Body();
        Text            = "WinScrobb — Updating";
        ControlBox      = false; // prevent manual close mid-download

        var icoPath = FluentTheme.FindAsset("icon.ico");
        if (icoPath != null) try { Icon = new Icon(icoPath); } catch { }

        // Title
        Controls.Add(new Label
        {
            Text      = $"Downloading WinScrobb {info.TagName}…",
            Font      = FluentTheme.Body(9.5f),
            ForeColor = FluentTheme.TextPrimary,
            AutoSize  = true,
            Location  = new Point(20, 20),
            BackColor = FluentTheme.Surface,
        });

        // Progress bar
        _bar = new ProgressBar
        {
            Location = new Point(20, 50),
            Size     = new Size(310, 18),
            Minimum  = 0,
            Maximum  = 100,
            Style    = ProgressBarStyle.Continuous,
        };
        Controls.Add(_bar);

        // Percentage label
        _pctLbl = new Label
        {
            Text      = "0%",
            Font      = FluentTheme.Body(9f),
            ForeColor = FluentTheme.TextMuted,
            AutoSize  = true,
            Location  = new Point(338, 52),
            BackColor = FluentTheme.Surface,
        };
        Controls.Add(_pctLbl);

        // Status sub-label
        _statusLbl = new Label
        {
            Text      = "Connecting…",
            Font      = FluentTheme.Caption(8f),
            ForeColor = FluentTheme.TextMuted,
            AutoSize  = false,
            Size      = new Size(360, 16),
            Location  = new Point(20, 80),
            BackColor = FluentTheme.Surface,
        };
        Controls.Add(_statusLbl);
    }

    public void SetProgress(int pct)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { Invoke(() => SetProgress(pct)); return; }

        _bar.Value  = Math.Clamp(pct, 0, 100);
        _pctLbl.Text = $"{pct}%";
        _statusLbl.Text = pct >= 100 ? "Launching installer…" : "Downloading update…";
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dark = FluentTheme.IsDarkMode() ? 1 : 0;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        int corner = 2;
        DwmSetWindowAttribute(Handle, 33, ref corner, sizeof(int));
    }
}
