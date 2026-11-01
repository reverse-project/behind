using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class SiteImageStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Add_ThenFind_RoundTripsTheImage()
    {
        byte[] bytes = [1, 2, 3, 4];
        Guid id;
        await using (var ctx = _factory.CreateContext())
        {
            id = await new SiteImageCommands(ctx).Add(new SiteImage("logo.png", "image/png", bytes, Now));
        }

        await using var read = _factory.CreateContext();
        var found = (await new SiteImageQueries(read).Find(id)).Match(() => throw new InvalidOperationException(), image => image);

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal("logo.png", found.FileName);
        Assert.Equal("image/png", found.ContentType);
        Assert.Equal(bytes, found.Content);
        Assert.Equal(Now, found.CreatedAt);
    }

    [Fact]
    public async Task Find_UnknownImage_ReturnsNone()
    {
        await using var ctx = _factory.CreateContext();

        Assert.Equal(None, await new SiteImageQueries(ctx).Find(Guid.NewGuid()));
    }

    [Fact]
    public async Task Add_NullImage_Throws()
    {
        await using var ctx = _factory.CreateContext();

        await Assert.ThrowsAsync<ArgumentNullException>(() => new SiteImageCommands(ctx).Add(null!));
    }
}
