using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace WinScrobb;

/// <summary>
/// Custom logo control that spins on each click. Used as the "tap me 32 times"
/// trigger for the retro-icon easter egg.
/// </summary>
public sealed class SpinningLogo : Control
{
    private Image? _img;
    private float  _angle;          // current displayed angle (degrees)
    private float  _targetAngle;    // angle we're animating toward
    private readonly System.Windows.Forms.Timer _tick;

    public event EventHandler? LogoClicked;

    public SpinningLogo()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Cursor    = Cursors.Hand;

        _tick = new System.Windows.Forms.Timer { Interval = 16 }; // ~60 fps
        _tick.Tick += (_, _) => StepAnimation();

        Click += (_, _) =>
        {
            _targetAngle += 360f;
            _tick.Start();
            LogoClicked?.Invoke(this, EventArgs.Empty);
        };
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Image? LogoImage
    {
        get => _img;
        set { _img = value; Invalidate(); }
    }

    private void StepAnimation()
    {
        // Ease toward target — fast at start, slow as it closes
        var diff = _targetAngle - _angle;
        if (Math.Abs(diff) < 0.4f)
        {
            _angle = _targetAngle;
            _tick.Stop();
        }
        else
        {
            _angle += diff * 0.18f;
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_img is null) return;

        var g = e.Graphics;
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        var cx = Width  / 2f;
        var cy = Height / 2f;

        var state = g.Save();
        g.TranslateTransform(cx, cy);
        g.RotateTransform(_angle);
        g.TranslateTransform(-cx, -cy);

        var fit = Math.Min(Width, Height);
        g.DrawImage(_img, (Width - fit) / 2f, (Height - fit) / 2f, fit, fit);

        g.Restore(state);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tick.Dispose();
        base.Dispose(disposing);
    }
}
