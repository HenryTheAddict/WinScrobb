namespace WinScrobb;

/// <summary>
/// Parses <c>iPod_Control/iTunes/Play Counts</c> — a small binary file written
/// by the iPod firmware containing one entry per track in iTunesDB order.
/// Entries record plays/skips that happened on the device since the last sync.
///
/// Format:
///   96-byte header:
///     0x00  "mhdp"
///     0x04  uint32 header_size  (always 0x60)
///     0x08  uint32 entry_size   (16, 28, 32, or 44 depending on firmware)
///     0x0C  uint32 num_entries
///   N entries of entry_size bytes, in the same order as mhit children of iTunesDB:
///     0x00  uint32 last_played  (Mac HFS time = seconds since 1904-01-01 UTC)
///     0x04  uint32 play_count   (plays since last sync — typically 1)
///     0x08  uint32 bookmark_ms
///     0x0C  uint32 rating
///     0x10  uint32 unknown
///     0x14  uint32 skip_count   (entry_size >= 24)
///     0x18  uint32 last_skipped (entry_size >= 28)
/// </summary>
public static class PlayCountsParser
{
    public record Entry(int TrackIndex, DateTime LastPlayed, uint PlayCount, uint SkipCount);

    public static List<Entry> Parse(string path)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length < 16) return [];

        // Magic
        if (data[0] != (byte)'m' || data[1] != (byte)'h' ||
            data[2] != (byte)'d' || data[3] != (byte)'p')
            return [];

        uint headerSize = U32(data, 0x04);
        uint entrySize  = U32(data, 0x08);
        uint count      = U32(data, 0x0C);

        if (entrySize is < 16 or > 256) return [];

        var entries = new List<Entry>((int)count);
        for (int i = 0; i < count; i++)
        {
            int off = (int)(headerSize + (uint)i * entrySize);
            if (off + entrySize > data.Length) break;

            uint lastPlayed = U32(data, off + 0x00);
            uint playCount  = U32(data, off + 0x04);
            uint skipCount  = entrySize >= 24 ? U32(data, off + 0x14) : 0;

            // Skip entries with no actual activity
            if (playCount == 0 && skipCount == 0) continue;
            if (lastPlayed == 0) continue;

            entries.Add(new Entry(i, MacTime(lastPlayed), playCount, skipCount));
        }

        return entries;
    }

    private static uint U32(byte[] b, int o) =>
        (uint)(b[o] | b[o + 1] << 8 | b[o + 2] << 16 | b[o + 3] << 24);

    // Mac HFS epoch: 1904-01-01 00:00:00 UTC
    private static readonly DateTime MacEpoch =
        new(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static DateTime MacTime(uint seconds) => MacEpoch.AddSeconds(seconds);
}
