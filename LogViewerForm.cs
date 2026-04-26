using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace WinScrobb;

/// <summary>
/// Modern log viewer — sticky header with logo + title, segmented filter chips
/// (All / Scrobbles / iPod / Errors / Skipped), live search, and syntax-coloured
/// monospace body.
/// </summary>
public sealed class LogViewerForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private enum FilterKind { All, Scrobbles, IPod, Errors, Skipped }

    private readonly RichTextBox _box;
    private readonly TextBox     _search;
    private readonly Label       _countLbl;
    private readonly List<FilterChip> _chips = [];
    private readonly IReadOnlyList<string> _entries;

    private string     _filter = "";
    private FilterKind _kind   = FilterKind.All;

    public LogViewerForm(IReadOnlyList<string> entries)
    {
        _entries = entries;

        Text          = "WinScrobb — Activity Log";
        Size          = new Size(820, 580);
        MinimumSize   = new Size(560, 380);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor     = FluentTheme.Surface;
        ForeColor     = FluentTheme.TextPrimary;
        Font          = FluentTheme.Body();

        var icoPath = FluentTheme.FindAsset("icon.ico");
        if (icoPath != null) try { Icon = new Icon(icoPath); } catch { }

        // ── Top bar (logo + title + count) ───────────────────────────────────
        var hdrBg  = FluentTheme.IsDarkMode() ? Color.FromArgb(28, 28, 32) : Color.FromArgb(238, 238, 241);
        var topBar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = hdrBg };

        var logoPath = FluentTheme.FindAsset("logosmall.png");
        if (logoPath != null)
            topBar.Controls.Add(new PictureBox
            {
                Image     = Image.FromFile(logoPath),
                SizeMode  = PictureBoxSizeMode.Zoom,
                Size      = new Size(26, 26),
                Location  = new Point(16, 15),
                BackColor = Color.Transparent,
            });

        topBar.Controls.Add(new Label
        {
            Text      = "Activity Log",
            Font      = FluentTheme.Subtitle(12f),
            ForeColor = FluentTheme.TextPrimary,
            AutoSize  = true,
            Location  = new Point(50, 16),
            BackColor = Color.Transparent,
        });

        _countLbl = new Label
        {
            Text      = "",
            Font      = FluentTheme.Caption(8.5f),
            ForeColor = FluentTheme.TextMuted,
            AutoSize  = true,
            BackColor = Color.Transparent,
        };
        topBar.Controls.Add(_countLbl);
        topBar.Layout += (_, _) =>
            _countLbl.Location = new Point(topBar.Width - _countLbl.Width - 18,
                                           (topBar.Height - _countLbl.Height) / 2);

        Controls.Add(topBar);

        // ── Toolbar (filter chips + search) ──────────────────────────────────
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = FluentTheme.Surface };

        AddChip(toolbar, "All",       FilterKind.All,       16);
        AddChip(toolbar, "Scrobbles", FilterKind.Scrobbles, 0);
        AddChip(toolbar, "iPod",      FilterKind.IPod,      0);
        AddChip(toolbar, "Errors",    FilterKind.Errors,    0);
        AddChip(toolbar, "Skipped",   FilterKind.Skipped,   0);
        UpdateChipSelection();

        _search = new TextBox
        {
            Font            = FluentTheme.Body(9.5f),
            BorderStyle     = BorderStyle.FixedSingle,
            BackColor       = FluentTheme.InputBg,
            ForeColor       = FluentTheme.TextPrimary,
            PlaceholderText = "Filter…",
            Width           = 220,
            Height          = 26,
        };
        _search.TextChanged += (_, _) => { _filter = _search.Text; Render(); };
        toolbar.Controls.Add(_search);

        toolbar.Layout += (_, _) =>
        {
            // Pack chips left-to-right
            int x = 16;
            foreach (var c in _chips)
            {
                c.Location = new Point(x, (toolbar.Height - c.Height) / 2);
                x = c.Right + 6;
            }
            _search.Location = new Point(toolbar.Width - _search.Width - 16,
                                         (toolbar.Height - _search.Height) / 2);
        };

        Controls.Add(toolbar);
        Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = FluentTheme.Divider });

        // ── Body ──────────────────────────────────────────────────────────────
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
        };
    }

    private void AddChip(Panel host, string label, FilterKind kind, int leftHint)
    {
        var chip = new FilterChip(label) { Tag = kind };
        chip.Click += (_, _) => { _kind = kind; UpdateChipSelection(); Render(); };
        _chips.Add(chip);
        host.Controls.Add(chip);
    }

    private void UpdateChipSelection()
    {
        foreach (var c in _chips)
            c.IsSelected = (FilterKind)c.Tag! == _kind;
    }

    private static readonly Regex TimestampRx =
        new(@"^(\[\d{2}:\d{2}:\d{2}\])(\s+)(\S?)", RegexOptions.Compiled);

    private void Render()
    {
        var lines = _entries.Where(MatchesFilters).ToList();

        _box.SuspendLayout();
        _box.Clear();

        if (lines.Count == 0)
        {
            _box.SelectionColor = FluentTheme.TextMuted;
            _box.AppendText(_entries.Count == 0
                ? "  (no log entries yet)"
                : "  (no entries match the current filter)");
        }
        else
        {
            foreach (var line in lines) AppendStyled(line);
        }
        _box.ResumeLayout();

        _countLbl.Text = lines.Count == _entries.Count
            ? $"{_entries.Count} entries"
            : $"{lines.Count} of {_entries.Count}";

        _box.SelectionStart = _box.Text.Length;
        _box.ScrollToCaret();
    }

    private bool MatchesFilters(string line)
    {
        if (!string.IsNullOrEmpty(_filter) &&
            !line.Contains(_filter, StringComparison.OrdinalIgnoreCase))
            return false;

        return _kind switch
        {
            FilterKind.All       => true,
            FilterKind.Scrobbles => line.Contains("Scrobbled", StringComparison.OrdinalIgnoreCase) ||
                                    line.Contains("Now playing") ||
                                    line.Contains("Now-playing sent"),
            FilterKind.IPod      => line.Contains("iPod",   StringComparison.OrdinalIgnoreCase) ||
                                    line.Contains("iTunes", StringComparison.OrdinalIgnoreCase),
            FilterKind.Errors    => line.Contains(" ✗ ") || line.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                                    line.Contains("error",  StringComparison.OrdinalIgnoreCase),
            FilterKind.Skipped   => line.Contains(" ⊘ "),
            _ => true,
        };
    }

    private void AppendStyled(string line)
    {
        var m = TimestampRx.Match(line);
        if (m.Success)
        {
            _box.SelectionColor = FluentTheme.Accent;
            _box.AppendText(m.Groups[1].Value);
            _box.SelectionColor = FluentTheme.TextPrimary;
            _box.AppendText(m.Groups[2].Value);

            var glyph = m.Groups[3].Value;
            _box.SelectionColor = glyph switch
            {
                "✓"  => Color.FromArgb(108, 198, 122),
                "✗"  => Color.FromArgb(232, 96, 96),
                "⊘"  => Color.FromArgb(232, 168, 88),
                "♥"  => Color.FromArgb(232, 86, 124),
                "→"  => FluentTheme.TextMuted,
                "✨" => Color.FromArgb(232, 196, 96),
                "👻" => Color.FromArgb(196, 160, 232),
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
        int dark = FluentTheme.IsDarkMode() ? 1 : 0;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
    }

    // ── Filter chip control ───────────────────────────────────────────────────

    private sealed class FilterChip : Control
    {
        private bool _selected;
        private bool _hover;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsSelected
        {
            get => _selected;
            set { _selected = value; Invalidate(); }
        }

        public FilterChip(string text)
        {
            Text   = text;
            Font   = FluentTheme.Body(9f);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;

            using var g = CreateGraphics();
            var sz = g.MeasureString(text, Font);
            Size = new Size((int)sz.Width + 24, 28);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = new GraphicsPath();
            int r = Height / 2;
            path.AddArc(rect.Left, rect.Top, r * 2, r * 2, 90, 180);
            path.AddArc(rect.Right - r * 2, rect.Top, r * 2, r * 2, 270, 180);
            path.CloseFigure();

            Color bg, border, fg;
            if (_selected)
            {
                bg     = FluentTheme.Accent;
                border = FluentTheme.Accent;
                fg     = Color.White;
            }
            else
            {
                bg     = _hover ? Color.FromArgb(FluentTheme.IsDarkMode() ? 60 : 230, 128, 128, 128)
                                : Color.Transparent;
                border = FluentTheme.Divider;
                fg     = FluentTheme.TextPrimary;
            }

            using (var fill = new SolidBrush(bg)) g.FillPath(fill, path);
            using (var pen  = new Pen(border, 1f)) g.DrawPath(pen, path);

            using var brush = new SolidBrush(fg);
            var sz = g.MeasureString(Text, Font);
            g.DrawString(Text, Font, brush, (Width - sz.Width) / 2f, (Height - sz.Height) / 2f);
        }
    }
}
