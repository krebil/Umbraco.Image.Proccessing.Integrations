using System.Buffers.Binary;
using Umbraco.Image.Processing.Core.Processing;

namespace Umbraco.Image.Processing.Core.Media;

/// <summary>
/// Reads dimensions and (JPEG-only) EXIF orientation straight from an image's container header,
/// without decoding pixels. Core stays processor-agnostic — no SkiaSharp/ImageSharp/Imageflow
/// dependency — while still being able to resolve the <c>cc</c> crop rectangle and orientation
/// before handing off to whichever <see cref="IImageProcessor" /> is active.
/// Supports JPEG, PNG, GIF, BMP, and WebP (VP8/VP8L/VP8X).
/// </summary>
public static class ImageHeaderReader
{
    public static bool TryRead(Stream stream, out ImageHeaderInfo info)
    {
        info = default;
        if (!stream.CanRead || !stream.CanSeek)
        {
            return false;
        }

        long start = stream.Position;
        try
        {
            Span<byte> sig = stackalloc byte[12];
            stream.Position = start;
            int read = ReadFully(stream, sig);

            if (read >= 2 && sig[0] == 0xFF && sig[1] == 0xD8)
            {
                stream.Position = start + 2;
                return TryReadJpeg(stream, out info);
            }

            if (read >= 8 && sig[0] == 0x89 && sig[1] == 'P' && sig[2] == 'N' && sig[3] == 'G')
            {
                stream.Position = start + 16; // 8-byte signature + 4-byte chunk length + "IHDR"
                return TryReadPng(stream, out info);
            }

            if (read >= 10 && sig[0] == 'G' && sig[1] == 'I' && sig[2] == 'F')
            {
                int width = BinaryPrimitives.ReadUInt16LittleEndian(sig[6..8]);
                int height = BinaryPrimitives.ReadUInt16LittleEndian(sig[8..10]);
                info = new ImageHeaderInfo(width, height, "gif", ExifOrientation.Unknown);
                return true;
            }

            if (read >= 2 && sig[0] == 'B' && sig[1] == 'M')
            {
                stream.Position = start + 18; // 14-byte file header + 4-byte DIB header size
                return TryReadBmp(stream, out info);
            }

            if (read >= 12 && sig[0] == 'R' && sig[1] == 'I' && sig[2] == 'F' && sig[3] == 'F' &&
                sig[8] == 'W' && sig[9] == 'E' && sig[10] == 'B' && sig[11] == 'P')
            {
                stream.Position = start + 12;
                return TryReadWebp(stream, out info);
            }

            return false;
        }
        finally
        {
            stream.Position = start;
        }
    }

    private static bool TryReadJpeg(Stream stream, out ImageHeaderInfo info)
    {
        info = default;
        ushort orientation = ExifOrientation.Unknown;
        var haveDims = false;
        int width = 0, height = 0;

        Span<byte> lenBuf = stackalloc byte[2];
        Span<byte> sof = stackalloc byte[5];

        while (true)
        {
            int b = stream.ReadByte();
            if (b == -1)
            {
                break;
            }

            if (b != 0xFF)
            {
                continue;
            }

            int marker = stream.ReadByte();
            while (marker == 0xFF)
            {
                marker = stream.ReadByte();
            }

            if (marker == -1)
            {
                break;
            }

            // Markers with no payload: SOI, TEM, RSTn.
            if (marker == 0x01 || marker is >= 0xD0 and <= 0xD8)
            {
                continue;
            }

            if (marker == 0xD9 || marker == 0xDA)
            {
                // EOI, or SOS — entropy-coded data follows, nothing more to scan.
                break;
            }

            if (ReadFully(stream, lenBuf) < 2)
            {
                break;
            }

            int length = BinaryPrimitives.ReadUInt16BigEndian(lenBuf);
            if (length < 2)
            {
                break;
            }

            int payloadLength = length - 2;
            long payloadStart = stream.Position;

            bool isSof = marker is (>= 0xC0 and <= 0xC3) or (>= 0xC5 and <= 0xC7) or (>= 0xC9 and <= 0xCB) or (>= 0xCD and <= 0xCF);
            if (isSof && payloadLength >= 5 && !haveDims)
            {
                if (ReadFully(stream, sof) == 5)
                {
                    height = BinaryPrimitives.ReadUInt16BigEndian(sof[1..3]);
                    width = BinaryPrimitives.ReadUInt16BigEndian(sof[3..5]);
                    haveDims = true;
                }
            }
            else if (marker == 0xE1 && payloadLength >= 8)
            {
                var app1 = new byte[payloadLength];
                if (ReadFully(stream, app1) == payloadLength)
                {
                    orientation = TryReadExifOrientation(app1) ?? orientation;
                }
            }

            stream.Position = payloadStart + payloadLength;

            if (haveDims && orientation != ExifOrientation.Unknown)
            {
                break;
            }
        }

        if (!haveDims)
        {
            return false;
        }

        info = new ImageHeaderInfo(width, height, "jpeg", orientation == ExifOrientation.Unknown ? ExifOrientation.TopLeft : orientation);
        return true;
    }

    private static ushort? TryReadExifOrientation(ReadOnlySpan<byte> app1Payload)
    {
        if (app1Payload.Length < 6 || app1Payload[0] != 'E' || app1Payload[1] != 'x' || app1Payload[2] != 'i' ||
            app1Payload[3] != 'f' || app1Payload[4] != 0 || app1Payload[5] != 0)
        {
            return null;
        }

        ReadOnlySpan<byte> tiff = app1Payload[6..];
        if (tiff.Length < 8)
        {
            return null;
        }

        bool littleEndian;
        if (tiff[0] == 'I' && tiff[1] == 'I')
        {
            littleEndian = true;
        }
        else if (tiff[0] == 'M' && tiff[1] == 'M')
        {
            littleEndian = false;
        }
        else
        {
            return null;
        }

        ushort magic = littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(tiff[2..4])
            : BinaryPrimitives.ReadUInt16BigEndian(tiff[2..4]);
        if (magic != 42)
        {
            return null;
        }

        uint ifdOffset = littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(tiff[4..8])
            : BinaryPrimitives.ReadUInt32BigEndian(tiff[4..8]);
        if (ifdOffset + 2 > tiff.Length)
        {
            return null;
        }

        ushort entryCount = littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(tiff.Slice((int)ifdOffset, 2))
            : BinaryPrimitives.ReadUInt16BigEndian(tiff.Slice((int)ifdOffset, 2));

        int entriesStart = (int)ifdOffset + 2;
        for (var i = 0; i < entryCount; i++)
        {
            int entryOffset = entriesStart + (i * 12);
            if (entryOffset + 12 > tiff.Length)
            {
                break;
            }

            ReadOnlySpan<byte> entry = tiff.Slice(entryOffset, 12);
            ushort tag = littleEndian
                ? BinaryPrimitives.ReadUInt16LittleEndian(entry[..2])
                : BinaryPrimitives.ReadUInt16BigEndian(entry[..2]);

            if (tag == 0x0112) // Orientation
            {
                return littleEndian
                    ? BinaryPrimitives.ReadUInt16LittleEndian(entry[8..10])
                    : BinaryPrimitives.ReadUInt16BigEndian(entry[8..10]);
            }
        }

        return null;
    }

    private static bool TryReadPng(Stream stream, out ImageHeaderInfo info)
    {
        info = default;
        Span<byte> dims = stackalloc byte[8];
        if (ReadFully(stream, dims) < 8)
        {
            return false;
        }

        int width = BinaryPrimitives.ReadInt32BigEndian(dims[..4]);
        int height = BinaryPrimitives.ReadInt32BigEndian(dims[4..8]);
        info = new ImageHeaderInfo(width, height, "png", ExifOrientation.Unknown);
        return true;
    }

    private static bool TryReadBmp(Stream stream, out ImageHeaderInfo info)
    {
        info = default;
        Span<byte> dims = stackalloc byte[8];
        if (ReadFully(stream, dims) < 8)
        {
            return false;
        }

        int width = BinaryPrimitives.ReadInt32LittleEndian(dims[..4]);
        int height = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(dims[4..8])); // negative height = top-down bitmap
        info = new ImageHeaderInfo(width, height, "bmp", ExifOrientation.Unknown);
        return true;
    }

    private static bool TryReadWebp(Stream stream, out ImageHeaderInfo info)
    {
        info = default;
        Span<byte> chunkHeader = stackalloc byte[8];
        if (ReadFully(stream, chunkHeader) < 8)
        {
            return false;
        }

        if (chunkHeader[0] == 'V' && chunkHeader[1] == 'P' && chunkHeader[2] == '8' && chunkHeader[3] == ' ')
        {
            Span<byte> payload = stackalloc byte[10];
            if (ReadFully(stream, payload) < 10)
            {
                return false;
            }

            if (payload[3] != 0x9D || payload[4] != 0x01 || payload[5] != 0x2A)
            {
                return false;
            }

            ushort w = BinaryPrimitives.ReadUInt16LittleEndian(payload[6..8]);
            ushort h = BinaryPrimitives.ReadUInt16LittleEndian(payload[8..10]);
            info = new ImageHeaderInfo(w & 0x3FFF, h & 0x3FFF, "webp", ExifOrientation.Unknown);
            return true;
        }

        if (chunkHeader[0] == 'V' && chunkHeader[1] == 'P' && chunkHeader[2] == '8' && chunkHeader[3] == 'L')
        {
            Span<byte> payload = stackalloc byte[5];
            if (ReadFully(stream, payload) < 5)
            {
                return false;
            }

            if (payload[0] != 0x2F)
            {
                return false;
            }

            int b0 = payload[1], b1 = payload[2], b2 = payload[3], b3 = payload[4];
            int width = 1 + (((b1 & 0x3F) << 8) | b0);
            int height = 1 + (((b3 & 0xF) << 10) | (b2 << 2) | ((b1 & 0xC0) >> 6));
            info = new ImageHeaderInfo(width, height, "webp", ExifOrientation.Unknown);
            return true;
        }

        if (chunkHeader[0] == 'V' && chunkHeader[1] == 'P' && chunkHeader[2] == '8' && chunkHeader[3] == 'X')
        {
            Span<byte> payload = stackalloc byte[10];
            if (ReadFully(stream, payload) < 10)
            {
                return false;
            }

            int width = 1 + (payload[4] | (payload[5] << 8) | (payload[6] << 16));
            int height = 1 + (payload[7] | (payload[8] << 8) | (payload[9] << 16));
            info = new ImageHeaderInfo(width, height, "webp", ExifOrientation.Unknown);
            return true;
        }

        return false;
    }

    private static int ReadFully(Stream stream, Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer[total..]);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}
