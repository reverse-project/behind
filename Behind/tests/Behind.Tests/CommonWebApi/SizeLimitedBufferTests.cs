using Behind.Common.WebApi;

namespace Behind.Tests.CommonWebApi;

public sealed class SizeLimitedBufferTests
{
    [Fact]
    public async Task ExactLimit_IsAcceptedButAnAdditionalByteIsRejected()
    {
        await using var buffer = new SizeLimitedBuffer(3);
        await buffer.WriteAsync(new byte[] { 1, 2, 3 });
        Assert.Equal(3, buffer.Length);
        Assert.Throws<PayloadLimitExceededException>(() => buffer.WriteByte(4));
        await Assert.ThrowsAsync<PayloadLimitExceededException>(
            async () => await buffer.WriteAsync(new byte[] { 4 }));
        Assert.Equal(3, buffer.Length);
    }

    [Fact]
    public void SeekingOrSettingLength_CannotBypassTheLimit()
    {
        using var buffer = new SizeLimitedBuffer(3);
        Assert.Throws<PayloadLimitExceededException>(() => buffer.SetLength(4));
        buffer.Position = 3;
        Assert.Throws<PayloadLimitExceededException>(() => buffer.Write([1], 0, 1));
        Assert.Equal(0, buffer.Length);
    }
}
