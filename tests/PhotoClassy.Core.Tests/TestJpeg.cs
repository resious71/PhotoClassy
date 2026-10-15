using System.Text;

namespace PhotoClassy.Core.Tests;

/// <summary>EXIF DateTimeOriginal 만 들어있는 최소 JPEG 바이트를 만든다.</summary>
public static class TestJpeg
{
    public static byte[] WithDateTimeOriginal(string exifDate)
    {
        var tiff = new List<byte>();
        void U16(int v) { tiff.Add((byte)v); tiff.Add((byte)(v >> 8)); }
        void U32(int v) { U16(v & 0xFFFF); U16((v >> 16) & 0xFFFF); }

        // TIFF header (little endian), IFD0 at offset 8
        tiff.AddRange(new byte[] { (byte)'I', (byte)'I', 0x2A, 0x00 });
        U32(8);
        // IFD0: ExifIFDPointer → 26
        U16(1);
        U16(0x8769); U16(4); U32(1); U32(26);
        U32(0);
        // Exif SubIFD: DateTimeOriginal (ASCII, 20) → 44
        U16(1);
        U16(0x9003); U16(2); U32(20); U32(44);
        U32(0);
        tiff.AddRange(Encoding.ASCII.GetBytes(exifDate.PadRight(19).Substring(0, 19)));
        tiff.Add(0);

        var app1 = new List<byte>();
        app1.AddRange(Encoding.ASCII.GetBytes("Exif"));
        app1.Add(0); app1.Add(0);
        app1.AddRange(tiff);

        var jpeg = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE1 };
        var length = app1.Count + 2;
        jpeg.Add((byte)(length >> 8));
        jpeg.Add((byte)length);
        jpeg.AddRange(app1);
        jpeg.AddRange(new byte[] { 0xFF, 0xD9 });
        return jpeg.ToArray();
    }
}
