using SkiaSharp;

namespace Behind.Tests.Common;

public class ImageThumbnailExtensionsTests
{
    private static ImageThumbnail Found(Option<ImageThumbnail> option) =>
        option.Match<ImageThumbnail>(() => throw new InvalidOperationException(), thumbnail => thumbnail);

    [Fact]
    public void ToThumbnail_ScalesTheLongSideDownAndKeepsTheAspectRatio()
    {
        var found = Found(TestImages.Png(800, 400).ToThumbnail(200));

        Assert.Equal(ImageThumbnailExtensions.ThumbnailContentType, found.ContentType);
        Assert.Equal((200, 100), TestImages.SizeOf(found.Content));
    }

    [Fact]
    public void ToThumbnail_ScalesTallImagesByTheirHeight()
    {
        var found = Found(TestImages.Jpeg(300, 900).ToThumbnail(300));

        Assert.Equal((100, 300), TestImages.SizeOf(found.Content));
    }

    [Fact]
    public void ToThumbnail_DoesNotEnlargeSmallImages()
    {
        var found = Found(TestImages.Png(50, 30).ToThumbnail(400));

        Assert.Equal((50, 30), TestImages.SizeOf(found.Content));
    }

    [Fact]
    public void ToThumbnail_KeepsTheColors()
    {
        var found = Found(TestImages.Png(100, 100, SKColors.Blue).ToThumbnail(40));

        var pixel = TestImages.PixelOf(found.Content, 10, 10);

        Assert.True(pixel.Blue > 200 && pixel.Red < 60 && pixel.Green < 60);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ToThumbnail_RejectsNonPositiveSizes(int side)
    {
        Assert.Equal(None, TestImages.Png(10, 10).ToThumbnail(side));
    }

    [Fact]
    public void ToThumbnail_ReturnsNoneForContentThatIsNotAnImage()
    {
        Assert.Equal(None, "not an image"u8.ToArray().ToThumbnail(100));
        Assert.Equal(None, Array.Empty<byte>().ToThumbnail(100));
    }

    [Fact]
    public void ToMosaic_ComposesTheImagesIntoASquare()
    {
        IReadOnlyList<byte[]> images =
        [
            TestImages.Png(200, 100, SKColors.Red),
            TestImages.Png(100, 200, SKColors.Lime),
            TestImages.Png(100, 100, SKColors.Blue),
            TestImages.Png(100, 100, SKColors.Yellow),
            TestImages.Png(100, 100, SKColors.White)
        ];

        var found = Found(images.ToMosaic(100));

        Assert.Equal((100, 100), TestImages.SizeOf(found.Content));

        var topLeft = TestImages.PixelOf(found.Content, 10, 10);
        var topRight = TestImages.PixelOf(found.Content, 90, 10);
        var bottomLeft = TestImages.PixelOf(found.Content, 10, 90);
        var bottomRight = TestImages.PixelOf(found.Content, 90, 90);

        Assert.True(topLeft.Red > 200 && topLeft.Green < 60);
        Assert.True(topRight.Green > 200 && topRight.Red < 60);
        Assert.True(bottomLeft.Blue > 200 && bottomLeft.Red < 60);
        Assert.True(bottomRight.Red > 200 && bottomRight.Green > 200 && bottomRight.Blue < 60);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ToMosaic_FillsTheSquareForFewerImages(int count)
    {
        IReadOnlyList<byte[]> images = Enumerable.Range(0, count).Select(_ => TestImages.Png(80, 60)).ToList();

        var found = Found(images.ToMosaic(64));

        Assert.Equal((64, 64), TestImages.SizeOf(found.Content));
        Assert.True(TestImages.PixelOf(found.Content, 60, 60).Red > 200);
    }

    [Fact]
    public void ToMosaic_SkipsUnreadableImages()
    {
        IReadOnlyList<byte[]> images = ["broken"u8.ToArray(), TestImages.Png(40, 40)];

        var found = Found(images.ToMosaic(32));

        Assert.Equal((32, 32), TestImages.SizeOf(found.Content));
    }

    [Fact]
    public void ToMosaic_ReturnsNoneWithoutReadableImages()
    {
        IReadOnlyList<byte[]> broken = ["broken"u8.ToArray()];

        Assert.Equal(None, broken.ToMosaic(32));
        Assert.Equal(None, ((IReadOnlyList<byte[]>)[]).ToMosaic(32));
        Assert.Equal(None, ((IReadOnlyList<byte[]>)[TestImages.Png(5, 5)]).ToMosaic(0));
    }

    [Theory]
    [InlineData(1, 100, 50)]
    [InlineData(3, 100, 50)]
    [InlineData(6, 50, 100)]
    [InlineData(8, 50, 100)]
    public void ToThumbnail_AppliesTheExifOrientation(ushort orientation, int width, int height)
    {
        var found = Found(TestImages.SplitJpeg(100, 50, orientation).ToThumbnail(400));

        Assert.Equal((width, height), TestImages.SizeOf(found.Content));
    }

    [Fact]
    public void ToThumbnail_RotatesTheLeftHalfToTheTopForAClockwiseOrientation()
    {
        var found = Found(TestImages.SplitJpeg(100, 50, 6).ToThumbnail(400));

        var top = TestImages.PixelOf(found.Content, 25, 10);
        var bottom = TestImages.PixelOf(found.Content, 25, 90);

        Assert.True(top.Red > 200 && top.Blue < 80);
        Assert.True(bottom.Blue > 200 && bottom.Red < 80);
    }
}
