using Behind.Core.Database;
using Behind.Core.Database.Queries;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class AuditLogFilterExtensionsTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<IReadOnlyList<string>> Search(Option<string> search)
    {
        await using var seed = _factory.CreateContext();
        await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed, entityName: "Role", entityKey: "alpha-1");
        await DatabaseSeedHelper.SeedAuditLogEntryAsync(seed, entityName: "User", entityKey: "beta-2");

        await using var context = _factory.CreateContext();
        var result = await new AuditLogEntryQueries(context, []).GetAuditLog(
            new IAuditLogEntryQueries.RequestAuditLog(
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                search.ToAuditLogFilter(),
                new Flipper(1, 20),
                new OrderByCollection()));

        return result.Match(
            () => (IReadOnlyList<string>)[],
            paged => paged.Results.Match(
                () => (IReadOnlyList<string>)[],
                items => items.Select(item => $"{item.EntityName}:{item.EntityKey}").Order().ToList()));
    }

    [Fact]
    public async Task NoSearch_KeepsEveryEntry() =>
        Assert.Equal(["Role:alpha-1", "User:beta-2"], await Search(None));

    [Fact]
    public async Task Search_MatchesTheEntityNameIgnoringCase() =>
        Assert.Equal(["Role:alpha-1"], await Search(Some("ROLE")));

    [Fact]
    public async Task Search_MatchesPartOfTheEntityKey() =>
        Assert.Equal(["User:beta-2"], await Search(Some("eta")));

    [Fact]
    public async Task Search_WithoutAMatch_FindsNothing() =>
        Assert.Empty(await Search(Some("missing")));
}
