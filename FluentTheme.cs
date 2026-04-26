using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinScrobb;

public static class FluentTheme
{
    // ── DWM interop ───────────────────────────────────────────────────────────

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins { public int Left, Right, Top, Bottom; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins m);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE      = 38;

    // ── Theme detection ───────────────────────────────────────────────────────

    public static bool IsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return (int)(key?.GetValue("AppsUseLightTheme") ?? 1) == 0;
        }
        catch { return false; }
    }

    // ── Apply DWM chrome ──────────────────────────────────────────────────────

    public static void ApplyChrome(Form form)
    {
        // Rounded corners (Win11)
        int corner = 2;
        DwmSetWindowAttribute(form.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        // Dark title bar follows system theme
        int dark = IsDarkMode() ? 1 : 0;
        DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

        // Mica backdrop (Win11 22H2+) — tints title bar & DWM chrome
        int mica = 2;
        DwmSetWindowAttribute(form.Handle, DWMWA_SYSTEMBACKDROP_TYPE, ref mica, sizeof(int));
    }

    // ── Color tokens (light / dark adaptive) ─────────────────────────────────

    public static Color Surface      => IsDarkMode() ? C(32,  32,  32)  : C(243, 243, 243);
    public static Color Card         => IsDarkMode() ? C(44,  44,  44)  : C(255, 255, 255);
    public static Color CardBorder   => IsDarkMode() ? C(60,  60,  60)  : C(224, 224, 224);
    public static Color Divider      => IsDarkMode() ? C(55,  55,  55)  : C(220, 220, 220);
    public static Color TextPrimary  => IsDarkMode() ? C(255, 255, 255) : C(28,  28,  28);
    public static Color TextMuted    => IsDarkMode() ? C(152, 152, 152) : C(100, 98,  96);
    public static Color InputBg      => IsDarkMode() ? C(50,  50,  50)  : C(252, 252, 252);
    public static Color InputBorder  => IsDarkMode() ? C(82,  82,  82)  : C(196, 196, 196);
    public static Color NeutralBtn   => IsDarkMode() ? C(56,  56,  56)  : C(254, 254, 254);
    public static Color NeutralBtnH  => IsDarkMode() ? C(66,  66,  66)  : C(246, 246, 246);
    public static Color NeutralBtnP  => IsDarkMode() ? C(48,  48,  48)  : C(238, 238, 238);
    public static Color Accent       => C(0,   120, 212);
    public static Color AccentHover  => C(16,  110, 190);
    public static Color AccentPress  => C(0,    98, 178);

    private static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    // ── Typography ────────────────────────────────────────────────────────────

    public static Font Display(float pt  = 20f) => Safe("Segoe UI Variable Display", pt);
    public static Font Subtitle(float pt = 12f) => Safe("Segoe UI Variable Display", pt, FontStyle.Bold);
    public static Font Body(float pt     = 10f) => Safe("Segoe UI Variable Text",    pt);
    public static Font Caption(float pt  = 8.5f)=> Safe("Segoe UI Variable Text",    pt);

    private static Font Safe(string family, float pt, FontStyle style = FontStyle.Regular)
    {
        try   { return new Font(family, pt, style); }
        catch { return new Font("Segoe UI", pt, style); }
    }

    // ── Icon helpers ──────────────────────────────────────────────────────────

    public static Icon PngToIcon(string path)
    {
        using var bmp = new Bitmap(path);
        return Icon.FromHandle(bmp.GetHicon());
    }

    // ── Asset resolution ──────────────────────────────────────────────────────

    public static string? FindAsset(string filename)
    {
        // Search in this order: assets/ next to exe → exe folder → repo root (debug) →
        // assets/ in repo root (debug). Returns first hit.
        var baseDir = AppContext.BaseDirectory;
        foreach (var candidate in new[]
        {
            Path.Combine(baseDir, "assets", filename),
            Path.Combine(baseDir, filename),
            Path.Combine(baseDir, "..", "..", "..", "assets", filename),
            Path.Combine(baseDir, "..", "..", "..", filename),
        })
            if (File.Exists(candidate)) return candidate;
        return null;
    }
}
