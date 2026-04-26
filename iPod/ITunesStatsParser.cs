namespace WinScrobb;

/// <summary>
/// Fallback parser for <c>iPod_Control/iTunes/iTunesStats</c>. Newer iPod
/// firmwares (Nano 3G+ in particular) sometimes write play activity here
/// instead of (or in addition to) <c>Play Counts</c>.
///
/// Layout is similar to Play Counts but uses 3-byte little-endian length-prefixed
/// variable-size entries:
///   16-byte header:
///     0x00  uint32  total_length
///     0x04  uint32  num_entries
///     0x08  ...     reserved
///   For each entry:
///     0x00  3 bytes  entry_size_le
///     0x03  3 bytes  bookmark_time
///     0x06  3 bytes  play_count_inc  (delta since last sync)
///     0x09  ...      remaining bytes — varies between firmwares
/// </summary>
public static class ITunesStatsParser
{
    public record Entry(int TrackIndex, uint PlayCountDelta, uint SkipCountDelta);

    public static List<Entry> Parse(string path)
    {
        var data = File.ReadAllBytes(path);
        var entries = new List<Entry>();
        if (data.Length < 16) return entries;

        // Header doesn't have a magic — sanity-check via num_entries
        uint totalLen   = U32(data, 0);
        uint numEntries = U32(data, 4);
        if (numEntries > 100_000) return entries; // likely not the format we expect

        int p = 16;
        for (int i = 0; i < numEntries && p + 9 <= data.Length; i++)
        {
            int entrySize  = (int)U24(data, p + 0);
            uint playDelta = U24(data, p + 6);
            uint skipDelta = entrySize >= 12 ? U24(data, p + 9) : 0;

            if (entrySize < 9 || entrySize > 1024) break;
            if (playDelta > 0 || skipDelta > 0)
                entries.Add(new Entry(i, playDelta, skipDelta));

            p += entrySize;
        }
        return entries;
    }

    private static uint U24(byte[] b, int o) =>
        (uint)(b[o] | b[o + 1] << 8 | b[o + 2] << 16);

    private static uint U32(byte[] b, int o) =>
        (uint)(b[o] | b[o + 1] << 8 | b[o + 2] << 16 | b[o + 3] << 24);
}
