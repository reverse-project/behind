using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class UserApprovalTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private Task<PagedResult<IUserApprovalQueries.PendingUser>> ListAsync(Option<string> search = default) =>
        _bed.WithUserApprovalQueriesAsync(queries => queries.ListPending(new Flipper(1, 10), search));

    private static IReadOnlyList<Guid> Ids(PagedResult<IUserApprovalQueries.PendingUser> page) =>
        page.Results.Match(() => [], items => items.Select(item => item.Id).ToList());

    [Fact]
    public async Task IsPending_IsFalseForUsersWithoutApprovalRequirement()
    {
        Assert.False(await _bed.WithUserApprovalQueriesAsync(queries => queries.IsPending(_bed.AuthorId)));
    }

    [Fact]
    public async Task RequireApproval_MakesTheUserPendingAndIsIdempotent()
    {
        await _bed.WithUserApprovalCommandsAsync(commands => commands.RequireApproval(_bed.AuthorId));
        await _bed.WithUserApprovalCommandsAsync(commands => commands.RequireApproval(_bed.AuthorId));

        Assert.True(await _bed.WithUserApprovalQueriesAsync(queries => queries.IsPending(_bed.AuthorId)));
        Assert.Equal([_bed.AuthorId], Ids(await ListAsync()));
    }

    [Fact]
    public async Task Approve_RemovesThePendingMark()
    {
        await _bed.WithUserApprovalCommandsAsync(commands => commands.RequireApproval(_bed.AuthorId));

        var approved = await _bed.WithUserApprovalCommandsAsync(commands => commands.Approve(_bed.AuthorId));

        Assert.True(approved);
        Assert.False(await _bed.WithUserApprovalQueriesAsync(queries => queries.IsPending(_bed.AuthorId)));
        Assert.Empty(Ids(await ListAsync()));
    }

    [Fact]
    public async Task Approve_UserThatIsNotPending_IsFalse()
    {
        Assert.False(await _bed.WithUserApprovalCommandsAsync(commands => commands.Approve(_bed.AuthorId)));
    }

    [Fact]
    public async Task ListPending_ListsNamesAndEmailsAndOnlyThePendingUsers()
    {
        await _bed.WithUserApprovalCommandsAsync(commands => commands.RequireApproval(_bed.OtherAuthorId));

        var page = await ListAsync();

        var user = Assert.Single(page.Results.GetOrElse([]));
        Assert.Equal(_bed.OtherAuthorId, user.Id);
        Assert.Equal("Test User", user.DisplayName);
        Assert.Equal(["other@test.com"], user.Emails);
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task ListPending_SearchesByNameOrEmail()
    {
        await _bed.WithUserApprovalCommandsAsync(commands => commands.RequireApproval(_bed.AuthorId));
        await _bed.WithUserApprovalCommandsAsync(commands => commands.RequireApproval(_bed.OtherAuthorId));

        Assert.Equal([_bed.OtherAuthorId], Ids(await ListAsync(Some("OTHER@"))));
        Assert.Empty(Ids(await ListAsync(Some("nobody"))));
        Assert.Equal(2, Ids(await ListAsync(Some("test user"))).Count);
    }

    [Fact]
    public async Task ListPending_LeavesOutSoftDeletedUsersButKeepsTheirPendingMark()
    {
        await _bed.WithUserApprovalCommandsAsync(commands => commands.RequireApproval(_bed.OtherAuthorId));

        await using (var ctx = _bed.Write())
        {
            await new UserCommands(ctx).Delete(_bed.OtherAuthorId);
        }

        Assert.Empty(Ids(await ListAsync()));
        Assert.True(await _bed.WithUserApprovalQueriesAsync(queries => queries.IsPending(_bed.OtherAuthorId)));
    }

    [Fact]
    public async Task RequireApproval_IsAuditedAsACreationOfTheUsersApproval()
    {
        await _bed.WithUserApprovalCommandsAsync(commands => commands.RequireApproval(_bed.AuthorId));

        await using var ctx = _bed.Read();
        var actions = await ctx.AuditLogEntries
            .Where(entry => entry.EntityName == UserApprovalStore.Table
                            && entry.EntityKey == _bed.AuthorId.ToString())
            .Select(entry => entry.Action)
            .ToListAsync();

        Assert.IsType<AuditLogEntry.Created>(Assert.Single(actions));
    }
}
