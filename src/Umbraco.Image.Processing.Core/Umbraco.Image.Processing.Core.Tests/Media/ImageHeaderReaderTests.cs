using System.Buffers.Binary;
using Umbraco.Image.Processing.Core.Media;
using Umbraco.Image.Processing.Core.Processing;
using Xunit;

namespace Umbraco.Image.Processing.Core.Tests.Media;

public class ImageHeaderReaderTests
{
    [Fact]
    public void ReadsPngDimensions()
    {
        using var stream = new MemoryStream(BuildPng(640, 480));

        Assert.True(ImageHeaderReader.TryRead(stream, out ImageHeaderInfo info));
        Assert.Equal(new ImageHeaderInfo(640, 480, "png", ExifOrientation.Unknown), info);
    }

    [Fact]
    public void ReadsGifDimensions()
    {
        using var stream = new MemoryStream(BuildGif(320, 200));

        Assert.True(ImageHeaderReader.TryRead(stream, out ImageHeaderInfo info));
        Assert.Equal(320, info.Width);
        Assert.Equal(200, info.Height);
        Assert.Equal("gif", info.Format);
    }

    [Fact]
    public void ReadsBmpDimensions()
    {
        using var stream = new MemoryStream(BuildBmp(100, 50));

        Assert.True(ImageHeaderReader.TryRead(stream, out ImageHeaderInfo info));
        Assert.Equal(100, info.Width);
        Assert.Equal(50, info.Height);
        Assert.Equal("bmp", info.Format);
    }

    [Fact]
    public void ReadsJpegDimensionsAndExifOrientation()
    {
        using var stream = new MemoryStream(BuildJpeg(64, 48, orientation: 6));

        Assert.True(ImageHeaderReader.TryRead(stream, out ImageHeaderInfo info));
        Assert.Equal(64, info.Width);
        Assert.Equal(48, info.Height);
        Assert.Equal("jpeg", info.Format);
        Assert.Equal((ushort)6, info.ExifOrientation);
    }

    [Fact]
    public void JpegWithoutExifDefaultsToTopLeft()
    {
        using var stream = new MemoryStream(BuildJpeg(64, 48, orientation: null));

        Assert.True(ImageHeaderReader.TryRead(stream, out ImageHeaderInfo info));
        Assert.Equal(ExifOrientation.TopLeft, info.ExifOrientation);
    }

    [Fact]
    public void RestoresTheOriginalStreamPosition()
    {
        using var stream = new MemoryStream(BuildPng(640, 480));
        stream.Position = 0;

        ImageHeaderReader.TryRead(stream, out _);

        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void ReturnsFalseForUnrecognizedData()
    {
        using var stream = new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);

        Assert.False(ImageHeaderReader.TryRead(stream, out _));
    }

    private static byte[] BuildPng(int width, int height)
    {
        var bytes = new byte[24];
        bytes[0] = 0x89; bytes[1] = (byte)'P'; bytes[2] = (byte)'N'; bytes[3] = (byte)'G';
        bytes[4] = 0x0D; bytes[5] = 0x0A; bytes[6] = 0x1A; bytes[7] = 0x0A;
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8, 4), 13); // IHDR chunk length
        bytes[12] = (byte)'I'; bytes[13] = (byte)'H'; bytes[14] = (byte)'D'; bytes[15] = (byte)'R';
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);
        return bytes;
    }

    private static byte[] BuildGif(int width, int height)
    {
        var bytes = new byte[10];
        bytes[0] = (byte)'G'; bytes[1] = (byte)'I'; bytes[2] = (byte)'F'; bytes[3] = (byte)'8'; bytes[4] = (byte)'9'; bytes[5] = (byte)'a';
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6, 2), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8, 2), (ushort)height);
        return bytes;
    }

    private static byte[] BuildBmp(int width, int height)
    {
        var bytes = new byte[26];
        bytes[0] = (byte)'B'; bytes[1] = (byte)'M';
        // bytes[2..14]: file size, reserved, data offset — unused by the reader.
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14, 4), 40); // DIB header size
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18, 4), width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22, 4), height);
        return bytes;
    }

    private static byte[] BuildJpeg(ushort width, ushort height, ushort? orientation)
    {
        using var stream = new MemoryStream();
        stream.WriteByte(0xFF);
        stream.WriteByte(0xD8); // SOI

        if (orientation is { } o)
        {
            byte[] exif =
            [
                (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0, // "Exif\0\0"
                (byte)'I', (byte)'I', // little-endian TIFF header
                42, 0, // magic
                8, 0, 0, 0, // offset to IFD0
                1, 0, // one entry
                0x12, 0x01, // tag 0x0112 = Orientation
                3, 0, // type SHORT
                1, 0, 0, 0, // count 1
                (byte)(o & 0xFF), (byte)(o >> 8), 0, 0, // value
                0, 0, 0, 0, // next IFD offset
            ];

            stream.WriteByte(0xFF);
            stream.WriteByte(0xE1); // APP1
            var len = (ushort)(exif.Length + 2);
            stream.WriteByte((byte)(len >> 8));
            stream.WriteByte((byte)(len & 0xFF));
            stream.Write(exif);
        }

        byte[] sof =
        [
            8, // precision
            (byte)(height >> 8), (byte)(height & 0xFF),
            (byte)(width >> 8), (byte)(width & 0xFF),
            1, // one component
            1, 0x11, 0, // component id, sampling factors, quant table
        ];

        stream.WriteByte(0xFF);
        stream.WriteByte(0xC0); // SOF0
        var sofLen = (ushort)(sof.Length + 2);
        stream.WriteByte((byte)(sofLen >> 8));
        stream.WriteByte((byte)(sofLen & 0xFF));
        stream.Write(sof);

        stream.WriteByte(0xFF);
        stream.WriteByte(0xD9); // EOI

        return stream.ToArray();
    }
}
