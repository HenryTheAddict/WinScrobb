namespace WinScrobb;

/// <summary>
/// Level-1 QuickLZ decompressor — sufficient for iPod <c>iTunesCDB</c> files.
///
/// Stream layout (the 9-byte QuickLZ header):
///   byte 0: header_byte
///       bit 0  set → data is compressed; clear → just raw bytes follow
///       bit 2  set → "streaming buffer" mode (length fields are 32-bit)
///   bytes 1..4: compressed size  (incl. these 9 header bytes)
///   bytes 5..8: decompressed size
///
/// In level 1, every 32 ops in the compressed stream are preceded by a
/// 4-byte little-endian "control word". Walking the bits LSB→MSB:
///   bit = 0  → copy 1 literal byte
///   bit = 1  → 2- or 3-byte match descriptor (offset/length encoded)
/// Whenever the control word is exhausted, the next 4 bytes become the new
/// control word.
///
/// Match decoding (level 1):
///   read u16 little-endian = m
///   length = ((m &amp; 0xF) | flags...) — see Decode() for the precise bit layout.
/// </summary>
public static class QuickLZ
{
    /// <summary>True if <paramref name="data"/> looks like a QuickLZ stream.</summary>
    public static bool LooksCompressed(byte[] data) =>
        data.Length >= 9 && (data[0] & 0x01) != 0;

    /// <summary>
    /// Decompresses an entire iTunesCDB blob. Throws on malformed streams.
    /// </summary>
    public static byte[] Decompress(byte[] src)
    {
        if (src.Length < 9) throw new InvalidDataException("QuickLZ: stream too short");

        byte header = src[0];
        bool compressed = (header & 0x01) != 0;
        bool stream     = (header & 0x02) != 0;

        // Length fields are always 32-bit LE for the headers we encounter on iPod
        int compSize = ReadInt32LE(src, 1);
        int decSize  = ReadInt32LE(src, 5);

        if (!compressed)
        {
            // Header says raw — just slice past the 9-byte header
            var raw = new byte[decSize];
            Buffer.BlockCopy(src, 9, raw, 0, Math.Min(decSize, src.Length - 9));
            return raw;
        }

        var dst    = new byte[decSize];
        int sp     = 9;             // source pointer (skip header)
        int dp     = 0;              // dest pointer

        uint control = 1;            // sentinel — re-fills on first use
        while (dp < decSize)
        {
            if (control == 1)
            {
                if (sp + 4 > src.Length)
                    throw new InvalidDataException("QuickLZ: control word out of range");
                control = ReadUInt32LE(src, sp);
                sp += 4;
                control |= 0x80000000u;   // sentinel bit so we know when 32 ops are done
            }

            if ((control & 1) != 0)
            {
                // ── Match ────────────────────────────────────────────────────
                if (sp + 2 > src.Length)
                    throw new InvalidDataException("QuickLZ: match header out of range");

                uint m = (uint)(src[sp] | (src[sp + 1] << 8));

                int matchlen;
                int offset;

                // QuickLZ level-1 match encoding:
                //   bits 0..3  → length-3 (so length is 3..18)
                //   bits 4..15 → offset (12-bit, 0..4095)
                // For longer matches a 3-byte form is used (bit 15 of m or extra byte).
                int lenField = (int)(m & 0x0F);
                offset       = (int)(m >> 4);
                if (lenField != 0)
                {
                    matchlen = lenField + 2;
                    sp += 2;
                }
                else
                {
                    // Extended length encoded in next byte (3 → 257)
                    if (sp + 3 > src.Length)
                        throw new InvalidDataException("QuickLZ: extended match length out of range");
                    matchlen = src[sp + 2] + 2;
                    sp += 3;
                }

                if (offset == 0 || offset > dp)
                    throw new InvalidDataException(
                        $"QuickLZ: bad back-reference (offset={offset}, dp={dp})");

                int from = dp - offset;
                for (int i = 0; i < matchlen; i++)
                    dst[dp++] = dst[from + i];
            }
            else
            {
                // ── Literal ──────────────────────────────────────────────────
                if (sp >= src.Length)
                    throw new InvalidDataException("QuickLZ: literal byte out of range");
                dst[dp++] = src[sp++];
            }

            control >>= 1;
        }

        return dst;
    }

    private static int  ReadInt32LE (byte[] b, int o) =>
        b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24);

    private static uint ReadUInt32LE(byte[] b, int o) =>
        (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
}
