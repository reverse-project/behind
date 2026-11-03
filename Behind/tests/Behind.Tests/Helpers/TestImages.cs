using SkiaSharp;

namespace Behind.Tests.Helpers;

public static class TestImages
{
    public static byte[] Png(int width, int height, SKColor? color = null) =>
        Encode(width, height, color ?? SKColors.Red, SKEncodedImageFormat.Png);

    public static byte[] Jpeg(int width, int height, SKColor? color = null) =>
        Encode(width, height, color ?? SKColors.Green, SKEncodedImageFormat.Jpeg);

    public static byte[] SplitJpeg(int width, int height, ushort orientation)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.Blue);

        using var paint = new SKPaint { Color = SKColors.Red };

        canvas.DrawRect(0, 0, width / 2f, height, paint);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);

        return WithOrientation(data.ToArray(), orientation);
    }

    public static (int Width, int Height) SizeOf(byte[] content)
    {
        using var bitmap = SKBitmap.Decode(content);

        return (bitmap.Width, bitmap.Height);
    }

    public static SKColor PixelOf(byte[] content, int x, int y)
    {
        using var bitmap = SKBitmap.Decode(content);

        return bitmap.GetPixel(x, y);
    }

    private static byte[] WithOrientation(byte[] jpeg, ushort orientation)
    {
        byte[] exif =
        [
            0xFF, 0xE1, 0x00, 0x22,
            0x45, 0x78, 0x69, 0x66, 0x00, 0x00,
            0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08,
            0x00, 0x01,
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, (byte)(orientation >> 8), (byte)orientation, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        ];

        return [.. jpeg[..2], .. exif, .. jpeg[2..]];
    }

    private static byte[] Encode(int width, int height, SKColor color, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(color);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);

        return data.ToArray();
    }
}
