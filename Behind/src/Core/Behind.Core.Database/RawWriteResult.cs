using Behind.Common.Database.Raw;

namespace Behind.Core.Database;

public abstract record RawWriteResult
{
    public sealed record Saved(RawRow Row) : RawWriteResult;

    public sealed record Deleted : RawWriteResult;

    public sealed record NotFound : RawWriteResult;

    public sealed record ReadOnly : RawWriteResult;

    public sealed record Invalid(IReadOnlyList<string> Errors) : RawWriteResult;

    public sealed record Rejected(string Message) : RawWriteResult;
}
