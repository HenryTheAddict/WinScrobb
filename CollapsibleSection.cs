using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace WinScrobb;

/// <summary>
/// A Fluent-style collapsible disclosure section. Click the header bar to
/// toggle visibility of the inner content. Designed for the Settings form.
///
/// Use:
///   var sect = new CollapsibleSection("Last.fm account") { Width = 460 };
///   sect.AddContent(myCardControl);   // anything you want inside
///   parent.Controls.Add(sect);
/// </summary>
public sealed class CollapsibleSection : Panel
{
    private readonly string _title;
    private readonly Panel  _header;
    private readonly Panel  _content;
    private readonly Label  _chevron;
    private readonly Label  _label;

    private bool _expanded;
    private int  _expandedHeight;
    private const int HeaderH = 36;

    private readonly System.Windows.Forms.Timer _anim = new() { Interval = 16 };
    private int _animTarget;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Expanded
    {
        get => _expanded;
        set { if (_expanded != value) Toggle(); }
    }

    public CollapsibleSection(string title, bool expanded = true)
    {
        _title    = title;
        _expanded = expanded;

        BackColor = Color.Transparent;
        Padding   = Padding.Empty;

        // ── Header (clickable bar) ────────────────────────────────────────────
        _header = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = HeaderH,
            BackColor = Color.Transparent,
            Cursor    = Cursors.Hand,
        };

        _chevron = new Label
        {
            Text      = expanded ? "" : "", // ChevronDown / ChevronRight (MDL2)
            Font      = new Font("Segoe MDL2 Assets", 8.5f),
            ForeColor = FluentTheme.TextMuted,
            AutoSize  = true,
            Location  = new Point(2, 11),
            BackColor = Color.Transparent,
        };
        _header.Controls.Add(_chevron);

        _label = new Label
        {
            Text      = title,
            Font      = FluentTheme.Body(9.5f),
            ForeColor = FluentTheme.TextMuted,
            AutoSize  = true,
            Location  = new Point(22, 9),
            BackColor = Color.Transparent,
        };
        _header.Controls.Add(_label);

        _header.Click  += (_, _) => Toggle();
        _chevron.Click += (_, _) => Toggle();
        _label.Click   += (_, _) => Toggle();
        Controls.Add(_header);

        // ── Content host ──────────────────────────────────────────────────────
        _content = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 0,
            BackColor = Color.Transparent,
            AutoSize  = false,
            Padding   = Padding.Empty,
        };
        Controls.Add(_content);

        Height = HeaderH; // collapsed by default; AddContent recalculates

        _anim.Tick += (_, _) => StepAnim();
    }

    /// <summary>Add a child control to the collapsible region. Stacks vertically.</summary>
    public void AddContent(Control c)
    {
        c.Top   = _content.Controls.Count == 0
            ? 8
            : _content.Controls[^1].Bottom + 12;
        _content.Controls.Add(c);

        // Recalculate the natural expanded height
        int max = 0;
        foreach (Control ctl in _content.Controls)
            if (ctl.Bottom > max) max = ctl.Bottom;
        _expandedHeight = max + 8;

        if (_expanded)
        {
            _content.Height = _expandedHeight;
            Height = HeaderH + _expandedHeight;
        }
    }

    private void Toggle()
    {
        _expanded = !_expanded;
        _chevron.Text = _expanded ? "" : "";
        _animTarget = _expanded ? _expandedHeight : 0;
        _anim.Start();
    }

    private void StepAnim()
    {
        var diff = _animTarget - _content.Height;
        if (Math.Abs(diff) <= 1)
        {
            _content.Height = _animTarget;
            _anim.Stop();
        }
        else
        {
            _content.Height += (int)Math.Round(diff * 0.22);
        }
        Height = HeaderH + _content.Height;
        // Notify parent so vertical stacking updates
        Parent?.PerformLayout();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // Subtle separator under the header
        using var pen = new Pen(FluentTheme.Divider, 1f);
        e.Graphics.DrawLine(pen, 0, HeaderH - 1, Width, HeaderH - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _anim.Dispose();
        base.Dispose(disposing);
    }
}
