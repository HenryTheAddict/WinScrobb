using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace WinScrobb;

// ── Shared geometry ───────────────────────────────────────────────────────────

internal static class Geom
{
    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X,         r.Y,          d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y,          d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
        p.AddArc(r.X,         r.Bottom - d, d, d,  90, 90);
        p.CloseFigure();
        return p;
    }
}

// ── FluentCard ────────────────────────────────────────────────────────────────
// Rounded card — Region clips children so no child can bleed into corners.

public class FluentCard : Panel
{
    private const float R = 8f;

    public FluentCard()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = FluentTheme.Card;
    }

    private void RefreshRegion()
    {
        if (Width > 0 && Height > 0)
        {
            using var path = Geom.RoundRect(new RectangleF(0, 0, Width, Height), R);
            Region = new Region(path);
        }
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); RefreshRegion(); }
    protected override void OnResize(EventArgs e)        { base.OnResize(e);         RefreshRegion(); }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Fill entire (clipped) area with card colour
        using var br = new SolidBrush(FluentTheme.Card);
        e.Graphics.FillRectangle(br, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode   = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        using var path = Geom.RoundRect(rect, R);
        using var pen  = new Pen(FluentTheme.CardBorder, 1f);
        g.DrawPath(pen, path);
    }
}

// ── FluentInput ───────────────────────────────────────────────────────────────

public class FluentInput : UserControl
{
    private readonly TextBox _tb     = new();
    private readonly Label   _toggle = new();
    private bool _focused;
    private bool _revealed;
    private const float R = 5f;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Value { get => _tb.Text; set => _tb.Text = value; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Font Font { get => _tb.Font; set { _tb.Font = value; UpdateLayout(); } }

    private bool _isPassword;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsPassword
    {
        get => _isPassword;
        set { _isPassword = value; _tb.UseSystemPasswordChar = value; _toggle.Visible = value; UpdateLayout(); }
    }

    public FluentInput()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 38;

        _tb.BorderStyle = BorderStyle.None;
        _tb.BackColor   = FluentTheme.InputBg;
        _tb.ForeColor   = FluentTheme.TextPrimary;
        _tb.Location    = new Point(11, 10);

        _tb.Enter  += (_, _) => { _focused = true;  Invalidate(); };
        _tb.Leave  += (_, _) => { _focused = false; Invalidate(); };
        _tb.KeyDown += (_, e) => OnKeyDown(e);

        // Segoe MDL2 Assets: E7B3 = view/eye, ED1A = hide
        _toggle.Text      = "";
        _toggle.Font      = new Font("Segoe MDL2 Assets", 10f);
        _toggle.ForeColor = FluentTheme.TextMuted;
        _toggle.AutoSize  = false;
        _toggle.Size      = new Size(28, 22);
        _toggle.TextAlign = ContentAlignment.MiddleCenter;
        _toggle.Cursor    = Cursors.Hand;
        _toggle.Visible   = false;
        _toggle.BackColor = Color.Transparent;
        _toggle.Click    += ToggleReveal;

        Controls.Add(_tb);
        Controls.Add(_toggle);
    }

    private void ToggleReveal(object? s, EventArgs e)
    {
        _revealed = !_revealed;
        _tb.UseSystemPasswordChar = !_revealed;
        _toggle.Text = _revealed ? "" : "";
        _tb.Focus();
    }

    private void UpdateLayout()
    {
        _tb.Width        = Width - 22 - (_toggle.Visible ? 32 : 0);
        _toggle.Location = new Point(Width - 34, (Height - _toggle.Height) / 2);
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); UpdateLayout(); }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Fill with parent colour so the rounded-rect corners aren't black voids
        using var br = new SolidBrush(Parent?.BackColor ?? FluentTheme.Surface);
        e.Graphics.FillRectangle(br, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode   = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        using var path = Geom.RoundRect(rect, R);

        var bg = FluentTheme.InputBg;
        _tb.BackColor = bg;
        using (var br = new SolidBrush(bg)) g.FillPath(br, path);

        using (var pen = new Pen(_focused ? FluentTheme.Accent : FluentTheme.InputBorder,
                                 _focused ? 1.5f : 1f))
            g.DrawPath(pen, path);

        if (_focused)
        {
            using var bar = new Pen(FluentTheme.Accent, 2f);
            g.DrawLine(bar, R + 1, Height - 1.5f, Width - R - 1, Height - 1.5f);
        }
    }
}

// ── FluentButton ──────────────────────────────────────────────────────────────

public class FluentButton : Control
{
    private Point _mouse   = Point.Empty;
    private bool  _hovered, _pressed;
    private bool  _isEnabled = true;
    private const float R = 5f;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsAccent { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new bool Enabled
    {
        get => _isEnabled;
        set { _isEnabled = value; Cursor = value ? Cursors.Hand : Cursors.Default; Invalidate(); }
    }

    public FluentButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        Height = 36;
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true;  Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e)  { if (_isEnabled && e.Button == MouseButtons.Left) { _pressed = true;  Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e)    { if (_pressed) { _pressed = false; Invalidate(); if (_isEnabled && ClientRectangle.Contains(e.Location)) OnClick(EventArgs.Empty); } base.OnMouseUp(e); }
    protected override void OnMouseMove(MouseEventArgs e) { _mouse = e.Location; if (_hovered) Invalidate(); base.OnMouseMove(e); }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var br = new SolidBrush(Parent?.BackColor ?? FluentTheme.Surface);
        e.Graphics.FillRectangle(br, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode   = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        using var path = Geom.RoundRect(rect, R);
        bool dark = FluentTheme.IsDarkMode();

        Color bg;
        if (!_isEnabled)
            bg = IsAccent ? Color.FromArgb(dark ? 55 : 150, FluentTheme.Accent)
                          : (dark ? Color.FromArgb(45,45,45) : Color.FromArgb(230,230,230));
        else if (IsAccent)
            bg = _pressed ? FluentTheme.AccentPress : _hovered ? FluentTheme.AccentHover : FluentTheme.Accent;
        else
            bg = _pressed ? FluentTheme.NeutralBtnP : _hovered ? FluentTheme.NeutralBtnH : FluentTheme.NeutralBtn;

        using (var br = new SolidBrush(bg)) g.FillPath(br, path);

        // Reveal highlight on neutral hover
        if (!IsAccent && _hovered && _isEnabled && !_pressed)
        {
            using var pgb = new PathGradientBrush((GraphicsPath)path.Clone())
            {
                CenterPoint    = new PointF(_mouse.X, _mouse.Y),
                CenterColor    = Color.FromArgb(dark ? 40 : 26, 255, 255, 255),
                SurroundColors = [Color.Transparent],
            };
            g.FillPath(pgb, path);
        }

        // Border
        using (var pen = new Pen(IsAccent ? Color.FromArgb(_pressed ? 20 : 40, 0, 0, 0)
                                          : (dark ? Color.FromArgb(68,68,68) : Color.FromArgb(196,196,196)), 1f))
            g.DrawPath(pen, path);

        // Top gloss
        if (!_pressed && _isEnabled)
        {
            using var gloss = new Pen(Color.FromArgb(IsAccent ? 52 : (dark ? 22 : 58), 255, 255, 255), 1f);
            g.DrawLine(gloss, R + 1, 1.5f, Width - R - 1, 1.5f);
        }

        var fg = !_isEnabled ? Color.FromArgb(110, IsAccent ? Color.White : FluentTheme.TextPrimary)
                             : (IsAccent ? Color.White : FluentTheme.TextPrimary);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

// ── TrayMenuItem ──────────────────────────────────────────────────────────────
// Full-width row for the tray popup: icon glyph + label, hover highlight.

public class TrayMenuItem : Control
{
    private bool _hovered;
    private const float R = 4f;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Glyph { get; set; } = "";

    public TrayMenuItem()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        Height = 40;
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true;  Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (ClientRectangle.Contains(e.Location)) OnClick(EventArgs.Empty);
        base.OnMouseUp(e);
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Always fill so text is never invisible against a black void
        using var br = new SolidBrush(FluentTheme.Surface);
        e.Graphics.FillRectangle(br, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode   = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        bool dark = FluentTheme.IsDarkMode();

        // Hover highlight
        if (_hovered)
        {
            var hoverRect = new RectangleF(6, 3, Width - 12, Height - 6);
            using var hPath = Geom.RoundRect(hoverRect, R);
            using var hBr   = new SolidBrush(dark ? Color.FromArgb(45, 255, 255, 255)
                                                   : Color.FromArgb(12, 0, 0, 0));
            g.FillPath(hBr, hPath);
        }

        // Glyph (Segoe MDL2 Assets)
        if (!string.IsNullOrEmpty(Glyph))
        {
            using var gFont = new Font("Segoe MDL2 Assets", 11f);
            TextRenderer.DrawText(g, Glyph, gFont,
                new Rectangle(14, 0, 32, Height), FluentTheme.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        // Label
        TextRenderer.DrawText(g, Text, Font,
            new Rectangle(string.IsNullOrEmpty(Glyph) ? 16 : 46, 0, Width - 56, Height),
            FluentTheme.TextPrimary,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}
