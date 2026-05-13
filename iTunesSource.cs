using System.Runtime.InteropServices;
using Windows.Media;

namespace WinScrobb;

/// <summary>
/// Falls back to iTunes COM automation when iTunes is running but not visible in SMTC.
/// Must be called before any await so it runs on the STA UI thread.
/// </summary>
public static class iTunesSource
{
    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(
        ref Guid rclsid,
        IntPtr pvReserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    // iTunes.Application CLSID
    private static readonly Guid iTunesCLSID = new("DC0C2640-1415-4644-875C-6F4D769839BA");

    private const int StatePlaying = 1;
    private const int FastForward  = 2;
    private const int Rewind       = 3;

    public static MediaSnapshot? GetSnapshot()
    {
        // ROT lookup only. A process-name gate is brittle across classic, Store,
        // and compatibility-launched iTunes builds; CreateInstance can relaunch
        // iTunes after the user intentionally quits.
        dynamic? iTunes = TryGetActiveObject();
        if (iTunes == null) return null;

        try
        {
            int state = (int)iTunes.PlayerState;
            bool playing = state is StatePlaying or FastForward or Rewind;

            dynamic? track = iTunes.CurrentTrack;
            if (track == null) return null;

            string title  = ReadString(() => track.Name);
            string artist = ReadString(() => track.Artist);
            string album  = ReadString(() => track.Album);

            if (string.IsNullOrWhiteSpace(artist))
                artist = ReadString(() => track.AlbumArtist);
            if (string.IsNullOrWhiteSpace(artist))
                artist = ReadString(() => track.Composer);

            if (string.IsNullOrWhiteSpace(title)) return null;

            double duration = 0, position = 0;
            try { duration = Convert.ToDouble(track.Duration); }        catch { }
            try { position = Convert.ToDouble(iTunes.PlayerPosition); } catch { }

            return new MediaSnapshot(
                Title:           title,
                Artist:          artist,
                Album:           album,
                IsPlaying:       playing,
                DurationSeconds: duration,
                PositionSeconds: position,
                PlaybackType:    MediaPlaybackType.Music,
                SourceApp:       "iTunes");
        }
        catch { return null; }
    }

    private static string ReadString(Func<dynamic?> read)
    {
        try { return Convert.ToString(read()) ?? ""; }
        catch { return ""; }
    }

    private static dynamic? TryGetActiveObject()
    {
        try
        {
            var guid = iTunesCLSID;
            GetActiveObject(ref guid, IntPtr.Zero, out object raw);
            return raw;
        }
        catch { return null; }
    }
}
