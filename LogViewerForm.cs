using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace WinScrobb;

/// <summary>
/// Modern log viewer — header bar with search + filter chips, monospace body
/// with timestamp + glyph syntax-highlighting, auto-scroll to bottom.
/// </summary>
public sealed class LogViewerForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private readonly RichTextBox _box;
    private readonly TextBox     _search;
    private readonly Label       _countLbl;
    private readonly IReadOnlyList<string> _entries;

    private string _filter = "";

    public LogViewerForm(IReadOnlyList<string> entries)
    {
        _entries = entries;

        Text          = "WinScrobb — Activity Log";
        Size          = new Size(720, 520);
        MinimumSize   = new Size(520, 360);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor     = FluentTheme.Surface;
        ForeColor     = FluentTheme.TextPrimary;
        Font          = FluentTheme.Body();

        var icoPath = FluentTheme.FindAsset("icon.ico");
        if (icoPath != null) try { Icon = new Icon(icoPath); } catch { }

        // ── Header bar (logo + title + search) ────────────────────────────────
        var hdrBg = FluentTheme.IsDarkMode() ? Color.FromArgb(28, 28, 32) : Color.FromArgb(238, 238, 241);
        var header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = hdrBg };

        var logoPath = FluentTheme.FindAsset("logosmall.png");
        if (logoPath != null)
            header.Controls.Add(new PictureBox
            {
                Image     = Image.FromFile(logoPath),
                SizeMode  = PictureBoxSizeMode.Zoom,
                Size      = new Size(24, 24),
                Location  = new Point(16, 16),
                BackColor = Color.Transparent,
            });

        header.Controls.Add(new Label
        {
            Text      = "Activity Log",
            Font      = FluentTheme.Subtitle(11.5f),
            ForeColor = FluentTheme.TextPrimary,
            AutoSize  = true,
            Location  = new Point(48, 18),
            BackColor = Color.Transparent,
        });

        _search = new TextBox
        {
            Font        = FluentTheme.Body(9.5f),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor   = FluentTheme.InputBg,
            ForeColor   = FluentTheme.TextPrimary,
            PlaceholderText = "Filter…",
            Width       = 220,
            Height      = 24,
        };
        _search.TextChanged += (_, _) => { _filter = _search.Text; Render(); };

        _countLbl = new Label
        {
            Text      = "",
            Font      = FluentTheme.Caption(8f),
            ForeColor = FluentTheme.TextMuted,
            AutoSize  = true,
            BackColor = Color.Transparent,
        };

        header.Controls.Add(_search);
        header.Controls.Add(_countLbl);
        header.Layout += (_, _) =>
        {
            _search.Location  = new Point(header.Width - _search.Width - 16, (header.Height - _search.Height) / 2);
            _countLbl.Location = new Point(_search.Left - _countLbl.Width - 12,
                                           (header.Height - _countLbl.Height) / 2);
        };

        Controls.Add(header);

        // Divider
        Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = FluentTheme.Divider });

        // ── Log body ─────────────────────────────────────────────────────────
        var bodyBg = FluentTheme.IsDarkMode() ? Color.FromArgb(20, 20, 22) : Color.FromArgb(248, 248, 250);

        _box = new RichTextBox
        {
            Dock        = DockStyle.Fill,
            ReadOnly    = true,
            BorderStyle = BorderStyle.None,
            BackColor   = bodyBg,
            ForeColor   = FluentTheme.TextPrimary,
            Font        = new Font("Cascadia Mono", 9.25f),
            ScrollBars  = RichTextBoxScrollBars.Vertical,
            WordWrap    = false,
            DetectUrls  = false,
        };
        Controls.Add(_box);

        Shown += (_, _) =>
        {
            Render();
            _box.SelectionStart = _box.Text.Length;
            _box.ScrollToCaret();
            _search.Focus();
        };
    }

    private void Render()
    {
        var lines = _entries
            .Where(l => string.IsNullOrEmpty(_filter) ||
                        l.Contains(_filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _box.SuspendLayout();
        _box.Clear();

        if (lines.Count == 0)
        {
            _box.SelectionColor = FluentTheme.TextMuted;
            _box.AppendText(string.IsNullOrEmpty(_filter)
                ? "  (no log entries yet)"
                : "  (no entries match the filter)");
        }
        else
        {
            foreach (var line in lines) AppendStyled(line);
        }
        _box.ResumeLayout();

        _countLbl.Text = lines.Count == _entries.Count
            ? $"{_entries.Count} entries"
            : $"{lines.Count} of {_entries.Count}";
    }

    /// <summary>Highlight [HH:mm:ss] timestamp + leading status glyph.</summary>
    private static readonly Regex TimestampRx = new(@"^(\[\d{2}:\d{2}:\d{2}\])(\s+)(\S?)", RegexOptions.Compiled);

    private void AppendStyled(string line)
    {
        var m = TimestampRx.Match(line);
        if (m.Success)
        {
            // Timestamp in accent
            _box.SelectionColor = FluentTheme.Accent;
            _box.AppendText(m.Groups[1].Value);

            _box.SelectionColor = FluentTheme.TextPrimary;
            _box.AppendText(m.Groups[2].Value);

            // Status glyph: ✓ green, ✗ red, ⊘ amber, ♥ pink, → muted
            var glyph = m.Groups[3].Value;
            _box.SelectionColor = glyph switch
            {
                "✓"  => Color.FromArgb(108, 198, 122),
                "✗"  => Color.FromArgb(232, 96, 96),
                "⊘"  => Color.FromArgb(232, 168, 88),
                "♥"  => Color.FromArgb(232, 86, 124),
                "→"  => FluentTheme.TextMuted,
                _    => FluentTheme.TextPrimary,
            };
            _box.AppendText(glyph);

            _box.SelectionColor = FluentTheme.TextPrimary;
            _box.AppendText(line[m.Length..]);
        }
        else
        {
            _box.SelectionColor = FluentTheme.TextPrimary;
            _box.AppendText(line);
        }
        _box.AppendText("\n");
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        FluentTheme.ApplyChrome(this);
        // Re-assert dark title bar (in case ApplyChrome timing differs)
        int dark = FluentTheme.IsDarkMode() ? 1 : 0;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
    }
}
