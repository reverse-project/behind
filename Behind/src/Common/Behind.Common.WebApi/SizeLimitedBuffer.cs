namespace Behind.Common.WebApi;

public sealed class PayloadLimitExceededException : IOException;

public sealed class SizeLimitedBuffer(long maximumBytes) : MemoryStream
{
    private void Check(long count)
    {
        if (count < 0 || Position > maximumBytes - count)
        {
            throw new PayloadLimitExceededException();
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Check(count);
        base.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Check(buffer.Length);
        base.Write(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Check(count);
        return base.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Check(buffer.Length);
        return base.WriteAsync(buffer, cancellationToken);
    }

    public override void WriteByte(byte value)
    {
        Check(1);
        base.WriteByte(value);
    }

    public override void SetLength(long value)
    {
        if (value < 0 || value > maximumBytes)
        {
            throw new PayloadLimitExceededException();
        }
        base.SetLength(value);
    }
}
