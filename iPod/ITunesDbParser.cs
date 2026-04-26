using System.Text;

namespace WinScrobb;

/// <summary>
/// Walks an iPod <c>iTunesDB</c> binary and extracts the master track list.
///
/// Top-level structure (all little-endian on Win-formatted iPods):
///   mhbd { header_size, total_size, ..., num_children } → followed by N mhsd
///   mhsd { header_size, total_size, type } where type=1 is the master track list
///   mhsd-type1 → mhlt { header_size, num_tracks } → N mhit
///   mhit { header_size=0x274, total_size, num_mhods, track_id, ..., last_played@0x5C, play_count@0x54 }
///         followed by num_mhods mhod children
///   mhod { header_size, total_size, type, ... payload }
///         For string types (1=Title, 3=Album, 4=Artist):
///           0x18 position
///           0x1C string_length_bytes
///           0x20 encoding (0=UTF-16 LE, 1=UTF-8)
///           0x24 unknown
///           0x28 string bytes
/// </summary>
public static class ITunesDbParser
{
    public static List<IPodTrack> Parse(string path)
    {
        var data = File.ReadAllBytes(path);

        // iTunesCDB: 9-byte QuickLZ header + compressed iTunesDB. Decompress in place.
        if (path.EndsWith("iTunesCDB", StringComparison.OrdinalIgnoreCase) ||
            QuickLZ.LooksCompressed(data))
        {
            data = QuickLZ.Decompress(data);
        }

        var tracks = new List<IPodTrack>();

        if (!Match(data, 0, "mhbd")) return tracks;

        uint mhbdHdr = U32(data, 4);

        // Walk mhsd children of the database
        int p = (int)mhbdHdr;
        while (p + 16 < data.Length)
        {
            if (!Match(data, p, "mhsd")) break;
            uint mhsdHdr   = U32(data, p + 4);
            uint mhsdTotal = U32(data, p + 8);
            uint mhsdType  = U32(data, p + 12);

            if (mhsdType == 1)
            {
                // Master track list
                ParseTrackList(data, p + (int)mhsdHdr, tracks);
            }

            if (mhsdTotal == 0) break;
            p += (int)mhsdTotal;
        }

        return tracks;
    }

    private static void ParseTrackList(byte[] data, int p, List<IPodTrack> tracks)
    {
        if (!Match(data, p, "mhlt")) return;
        uint mhltHdr  = U32(data, p + 4);
        uint numTracks = U32(data, p + 8);

        p += (int)mhltHdr;
        for (int i = 0; i < numTracks && p + 16 < data.Length; i++)
        {
            if (!Match(data, p, "mhit")) break;
            var (track, advance) = ParseMhit(data, p);
            if (track is not null) tracks.Add(track);
            if (advance == 0) break;
            p += advance;
        }
    }

    private static (IPodTrack? track, int advance) ParseMhit(byte[] data, int offset)
    {
        if (!Match(data, offset, "mhit")) return (null, 0);
        uint hdrSize  = U32(data, offset + 4);
        uint totSize  = U32(data, offset + 8);
        uint numMhods = U32(data, offset + 12);
        uint trackId  = U32(data, offset + 16);

        // Field offsets within the mhit fixed header
        int  lengthMs   = (int)U32(data, offset + 0x2C);
        uint playCount  = U32(data, offset + 0x54);
        uint lastPlayed = U32(data, offset + 0x5C);

        string title = "", artist = "", album = "", genre = "";

        int p = offset + (int)hdrSize;
        for (int i = 0; i < numMhods && p + 24 < data.Length; i++)
        {
            if (!Match(data, p, "mhod")) break;
            uint mhodHdr   = U32(data, p + 4);
            uint mhodTotal = U32(data, p + 8);
            uint mhodType  = U32(data, p + 12);

            if (mhodType is 1 or 3 or 4 or 5 && mhodHdr >= 0x18)
            {
                var s = ReadMhodString(data, p);
                switch (mhodType)
                {
                    case 1: title  = s; break;
                    case 3: album  = s; break;
                    case 4: artist = s; break;
                    case 5: genre  = s; break;
                }
            }

            if (mhodTotal == 0) break;
            p += (int)mhodTotal;
        }

        var track = new IPodTrack
        {
            TrackId    = trackId,
            Title      = title,
            Artist     = artist,
            Album      = album,
            Genre      = genre,
            LengthMs   = lengthMs,
            PlayCount  = playCount,
            LastPlayed = lastPlayed == 0 ? null : PlayCountsParser.MacTime(lastPlayed),
        };

        return (track, (int)totSize);
    }

    private static string ReadMhodString(byte[] data, int mhodOffset)
    {
        try
        {
            int strLen = (int)U32(data, mhodOffset + 0x1C);
            int enc    = (int)U32(data, mhodOffset + 0x20);
            int start  = mhodOffset + 0x28;

            if (strLen <= 0 || strLen > 4096) return "";
            if (start + strLen > data.Length) return "";

            return enc == 1
                ? Encoding.UTF8.GetString(data, start, strLen)
                : Encoding.Unicode.GetString(data, start, strLen);
        }
        catch { return ""; }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool Match(byte[] b, int o, string magic)
    {
        if (o + 4 > b.Length) return false;
        for (int i = 0; i < 4; i++)
            if (b[o + i] != (byte)magic[i]) return false;
        return true;
    }

    private static uint U32(byte[] b, int o) =>
        (uint)(b[o] | b[o + 1] << 8 | b[o + 2] << 16 | b[o + 3] << 24);
}

public record IPodTrack
{
    public required uint     TrackId    { get; init; }
    public required string   Title      { get; init; }
    public required string   Artist     { get; init; }
    public required string   Album      { get; init; }
    public          string   Genre      { get; init; } = "";
    public          int      LengthMs   { get; init; }
    public          uint     PlayCount  { get; init; }
    public          DateTime? LastPlayed { get; init; }

    public bool HasMetadata => !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Artist);
    public int  DurationSec => LengthMs / 1000;
}
