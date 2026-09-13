using Behind.Common.Database.Generators;

namespace Behind.Tests.CommonDatabase;

public class Uuid7ValueGeneratorTests
{
    // The implementation is: Guid.CreateVersion7() — it does NOT use the entry parameter.

    [Fact]
    public void GeneratesTemporaryValues_IsFalse()
    {
        var gen = new Uuid7ValueGenerator();
        Assert.False(gen.GeneratesTemporaryValues);
    }

    [Fact]
    public void Next_ReturnsNonEmptyGuid()
    {
        var gen = new Uuid7ValueGenerator();
        var guid = gen.Next(null!);
        Assert.NotEqual(Guid.Empty, guid);
    }

    [Fact]
    public void Next_TwoSuccessiveCalls_ReturnDifferentGuids()
    {
        var gen = new Uuid7ValueGenerator();
        var g1 = gen.Next(null!);
        var g2 = gen.Next(null!);
        Assert.NotEqual(g1, g2);
    }

    [Fact]
    public void Next_ReturnsVersion7Guid()
    {
        // UUID v7: the 4 most-significant bits of the 7th byte (0-indexed) equal 0x7
        var gen = new Uuid7ValueGenerator();
        var guid = gen.Next(null!);

        // Extract byte 6 in big-endian layout
        Span<byte> bytes = stackalloc byte[16];
        guid.TryWriteBytes(bytes, bigEndian: true, out _);

        // High nibble of byte 6 must be 7 for UUID v7
        int version = (bytes[6] >> 4) & 0xF;
        Assert.Equal(7, version);
    }
}
