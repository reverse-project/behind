using SkiaSharp;

namespace Behind.Common;

public static class CaptchaImageExtensions
{
    public const string ContentType = "image/png";
    public const int Width = 200;
    public const int Height = 70;

    private const float CellSize = 6f;
    private const int StrikeCount = 4;
    private const int DotCount = 120;
    private const float MaxRotation = 18f;
    private const float MaxSkew = 0.3f;
    private static readonly SKColor Background = new(0xF4, 0xF4, 0xF0);

    extension(string code)
    {
        public byte[] ToCaptchaImage()
        {
            using var bitmap = new SKBitmap(Width, Height);
            using var canvas = new SKCanvas(bitmap);

            canvas.Clear(Background);
            canvas.DrawDots(DotCount);
            canvas.DrawCode(code);
            canvas.DrawStrikes(StrikeCount);

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);

            return data.ToArray();
        }
    }

    extension(SKCanvas canvas)
    {
        private void DrawCode(string code)
        {
            var slot = (float)Width / Math.Max(code.Length, 1);

            foreach (var (character, index) in code.Select((character, index) => (character, index)))
            {
                canvas.DrawGlyph(character, slot * (index + 0.5f), Height / 2f + Between(-6f, 6f));
            }
        }

        private void DrawGlyph(char character, float centerX, float centerY)
        {
            using var paint = new SKPaint { Color = DarkColor(), IsAntialias = true };

            canvas.Save();
            canvas.Translate(centerX, centerY);
            canvas.RotateDegrees(Between(-MaxRotation, MaxRotation));
            canvas.Skew(Between(-MaxSkew, MaxSkew), 0f);

            foreach (var (column, row) in character.ToGlyphCells())
            {
                var left = (column - CaptchaGlyphs.Columns / 2f) * CellSize;
                var top = (row - CaptchaGlyphs.Rows / 2f) * CellSize;

                canvas.DrawRoundRect(
                    new SKRect(left, top, left + CellSize + 1f, top + CellSize + 1f),
                    1.5f,
                    1.5f,
                    paint);
            }

            canvas.Restore();
        }

        private void DrawStrikes(int count)
        {
            foreach (var _ in Enumerable.Range(0, count))
            {
                using var paint = new SKPaint
                {
                    Color = DarkColor().WithAlpha(200),
                    StrokeWidth = Between(1.5f, 2.5f),
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke
                };

                using var builder = new SKPathBuilder();

                builder.MoveTo(0f, Between(0f, Height));
                builder.CubicTo(
                    Width / 3f,
                    Between(0f, Height),
                    2f * Width / 3f,
                    Between(0f, Height),
                    Width,
                    Between(0f, Height));

                using var path = builder.Detach();

                canvas.DrawPath(path, paint);
            }
        }

        private void DrawDots(int count)
        {
            foreach (var _ in Enumerable.Range(0, count))
            {
                using var paint = new SKPaint { Color = DarkColor().WithAlpha(120), IsAntialias = true };

                canvas.DrawCircle(Between(0f, Width), Between(0f, Height), Between(0.8f, 2f), paint);
            }
        }
    }

    private static float Between(float minimum, float maximum) =>
        minimum + (float)Random.Shared.NextDouble() * (maximum - minimum);

    private static SKColor DarkColor() =>
        new((byte)Random.Shared.Next(0, 110), (byte)Random.Shared.Next(0, 110), (byte)Random.Shared.Next(0, 110));
}
