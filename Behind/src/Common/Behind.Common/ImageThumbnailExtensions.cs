using SkiaSharp;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common;

public sealed record ImageThumbnail(byte[] Content, string ContentType);

public static class ImageThumbnailExtensions
{
    public const string ThumbnailContentType = "image/webp";
    public const long MaxPixels = 100_000_000;

    private const int Quality = 80;
    private const int MaxMosaicImages = 4;
    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);
    private static readonly SKColor Background = new(0x21, 0x25, 0x29);

    extension(byte[] image)
    {
        public Option<ImageThumbnail> ToThumbnail(int maxSide) =>
            maxSide <= 0
                ? None
                : Try(() => image.CreateThumbnail(maxSide))
                    .Run()
                    .Match<Option<ImageThumbnail>>(_ => None, thumbnail => thumbnail);

        private Option<ImageThumbnail> CreateThumbnail(int maxSide) =>
            image.Decode().Match<Option<ImageThumbnail>>(
                () => None,
                bitmap =>
                {
                    using (bitmap)
                    {
                        using var fitted = bitmap.Fit(maxSide);

                        return fitted.Encode();
                    }
                });

        private Option<SKBitmap> Decode()
        {
            using var data = SKData.CreateCopy(image);
            using var codec = SKCodec.Create(data);

            if (codec is null || (long)codec.Info.Width * codec.Info.Height > MaxPixels)
            {
                return None;
            }

            var decoded = SKBitmap.Decode(codec);

            if (decoded is null)
            {
                return None;
            }

            using (decoded)
            {
                return decoded.Oriented(codec.EncodedOrigin);
            }
        }
    }

    extension(IReadOnlyList<byte[]> images)
    {
        public Option<ImageThumbnail> ToMosaic(int side) =>
            side <= 0 || images.Count == 0
                ? None
                : Try(() => images.CreateMosaic(side))
                    .Run()
                    .Match<Option<ImageThumbnail>>(_ => None, mosaic => mosaic);

        private Option<ImageThumbnail> CreateMosaic(int side)
        {
            var bitmaps = images
                .Take(MaxMosaicImages)
                .Select(image => image.Decode())
                .SelectMany(decoded => decoded.AsEnumerable())
                .ToList();

            try
            {
                return bitmaps.Count == 0
                    ? None
                    : bitmaps.Compose(side).Encode();
            }
            finally
            {
                bitmaps.ForEach(bitmap => bitmap.Dispose());
            }
        }
    }

    extension(IReadOnlyList<SKBitmap> bitmaps)
    {
        private SKBitmap Compose(int side)
        {
            var composed = new SKBitmap(side, side);
            using var canvas = new SKCanvas(composed);
            using var paint = new SKPaint { IsAntialias = true };

            canvas.Clear(Background);

            foreach (var (bitmap, cell) in bitmaps.Zip(Cells(bitmaps.Count, side)))
            {
                canvas.DrawCover(bitmap, cell, paint);
            }

            return composed;
        }
    }

    extension(SKCanvas canvas)
    {
        private void DrawCover(SKBitmap bitmap, SKRect cell, SKPaint paint)
        {
            var scale = Math.Max(cell.Width / bitmap.Width, cell.Height / bitmap.Height);
            var width = cell.Width / scale;
            var height = cell.Height / scale;
            var left = (bitmap.Width - width) / 2;
            var top = (bitmap.Height - height) / 2;

            using var image = SKImage.FromBitmap(bitmap);

            canvas.DrawImage(
                image,
                new SKRect(left, top, left + width, top + height),
                cell,
                Sampling,
                paint);
        }
    }

    extension(SKBitmap bitmap)
    {
        private Option<SKBitmap> Oriented(SKEncodedOrigin origin)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            var swaps = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
                or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

            var target = new SKBitmap(swaps ? height : width, swaps ? width : height);

            using var canvas = new SKCanvas(target);

            canvas.Clear(SKColors.Transparent);
            canvas.Concat(OriginMatrix(origin, width, height));

            using var image = SKImage.FromBitmap(bitmap);

            canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default);

            return target;
        }

        private SKBitmap Fit(int maxSide)
        {
            var scale = Math.Min(1d, (double)maxSide / Math.Max(bitmap.Width, bitmap.Height));

            if (scale >= 1d)
            {
                return bitmap.Copy();
            }

            var info = new SKImageInfo(
                Math.Max(1, (int)Math.Round(bitmap.Width * scale)),
                Math.Max(1, (int)Math.Round(bitmap.Height * scale)));

            return bitmap.Resize(info, Sampling) ?? bitmap.Copy();
        }

        private Option<ImageThumbnail> Encode()
        {
            using var encoded = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Webp, Quality);

            return encoded is null
                ? None
                : Some(new ImageThumbnail(encoded.ToArray(), ThumbnailContentType));
        }
    }

    private static IReadOnlyList<SKRect> Cells(int count, int side)
    {
        float full = side;
        var half = full / 2;

        return count switch
        {
            1 => [new SKRect(0, 0, full, full)],
            2 => [new SKRect(0, 0, half, full), new SKRect(half, 0, full, full)],
            3 =>
            [
                new SKRect(0, 0, half, full),
                new SKRect(half, 0, full, half),
                new SKRect(half, half, full, full)
            ],
            _ =>
            [
                new SKRect(0, 0, half, half),
                new SKRect(half, 0, full, half),
                new SKRect(0, half, half, full),
                new SKRect(half, half, full, full)
            ]
        };
    }

    private static SKMatrix OriginMatrix(SKEncodedOrigin origin, int width, int height) =>
        origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, height, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, height, -1, 0, width, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, width, 0, 0, 1),
            _ => SKMatrix.Identity
        };
}
