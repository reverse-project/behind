using SkiaSharp;

namespace Behind.Tests.Common;

public class CaptchaImageExtensionsTests
{
    [Fact]
    public void ToCaptchaImage_RendersAPngOfTheDocumentedSize()
    {
        var image = "AB2C9".ToCaptchaImage();

        using var bitmap = SKBitmap.Decode(image);

        Assert.Equal((CaptchaImageExtensions.Width, CaptchaImageExtensions.Height), (bitmap.Width, bitmap.Height));
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, image[..4]);
    }

    [Fact]
    public void ToCaptchaImage_DistortsEveryRender()
    {
        Assert.NotEqual("AB2C9".ToCaptchaImage(), "AB2C9".ToCaptchaImage());
    }

    [Fact]
    public void Alphabet_HasAGlyphForEveryCharacter()
    {
        Assert.All(
            CaptchaGlyphs.Alphabet,
            character => Assert.NotEmpty(character.ToGlyphCells()));
    }

    [Fact]
    public void Alphabet_KeepsEveryGlyphInsideTheGrid()
    {
        Assert.All(
            CaptchaGlyphs.Alphabet.SelectMany(character => character.ToGlyphCells()),
            cell =>
            {
                Assert.InRange(cell.Column, 0, CaptchaGlyphs.Columns - 1);
                Assert.InRange(cell.Row, 0, CaptchaGlyphs.Rows - 1);
            });
    }

    [Fact]
    public void Alphabet_HasNoLookAlikeCharactersAndNoDuplicates()
    {
        Assert.DoesNotContain(CaptchaGlyphs.Alphabet, character => "01IO".Contains(character));
        Assert.Equal(CaptchaGlyphs.Alphabet.Length, CaptchaGlyphs.Alphabet.Distinct().Count());
    }

    [Fact]
    public void Alphabet_GivesEveryCharacterItsOwnShape()
    {
        var shapes = CaptchaGlyphs.Alphabet.Select(character => string.Join(",", character.ToGlyphCells())).ToArray();

        Assert.Equal(shapes.Length, shapes.Distinct().Count());
    }

    [Fact]
    public void ToGlyphCells_IsEmptyForUnknownCharacters()
    {
        Assert.Empty('?'.ToGlyphCells());
    }
}
