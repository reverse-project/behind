using Behind.Core.Database;
using Behind.Core.Database.Aggregates;
using Microsoft.EntityFrameworkCore;
using static Behind.Tests.Helpers.RouteAliasTestBedExtensions;

namespace Behind.Tests.CoreDatabase;

public sealed class RouteAliasCommandsTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<IReadOnlyList<(long Version, string Alias, string Target)>> VersionsAsync(Guid key)
    {
        await using var ctx = _bed.Read();
        var rows = await ctx.RouteAliases
            .OfKey(key)
            .OrderBy(alias => EF.Property<long>(alias, VersionedColumns.Version))
            .Select(alias => new { Version = EF.Property<long>(alias, VersionedColumns.Version), alias.Alias, alias.Target })
            .ToListAsync();

        return rows.Select(row => (row.Version, row.Alias, row.Target)).ToList();
    }

    [Fact]
    public async Task Create_StoresVersionOneAndAuditsTheCreation()
    {
        var key = await _bed.CreateRouteAliasAsync("manuals", "#/downloads/manuals");

        Assert.Equal([(1L, "manuals", "#/downloads/manuals")], await VersionsAsync(key));
        Assert.Equal(["Created"], await _bed.ActionsAsync("RouteAlias", key));
    }

    [Fact]
    public async Task Create_DuplicateAlias_IsRejected()
    {
        await _bed.CreateRouteAliasAsync("manuals");

        Assert.IsType<RouteAliasSaveResult.AliasTaken>(
            await _bed.WithRouteAliasCommandsAsync(commands => commands.Create(Draft("manuals", "#/news"))));
    }

    [Fact]
    public async Task Update_InsertsTheNextVersionAndKeepsTheOldOne()
    {
        var key = await _bed.CreateRouteAliasAsync("manuals", "#/downloads/a");

        var result = await _bed.WithRouteAliasCommandsAsync(commands => commands.Update(key, Draft("docs", "#/downloads/b")));

        Assert.Equal(new VersionedKey<Guid>(key, 2), Assert.IsType<RouteAliasSaveResult.Saved>(result).Id);
        Assert.Equal([(1L, "manuals", "#/downloads/a"), (2L, "docs", "#/downloads/b")], await VersionsAsync(key));
    }

    [Fact]
    public async Task Update_KeepingItsOwnAlias_IsAllowed()
    {
        var key = await _bed.CreateRouteAliasAsync("manuals", "#/downloads/a");

        Assert.IsType<RouteAliasSaveResult.Saved>(
            await _bed.WithRouteAliasCommandsAsync(commands => commands.Update(key, Draft("manuals", "#/downloads/b"))));
    }

    [Fact]
    public async Task Update_AliasOfAnotherRouteAlias_IsRejected()
    {
        await _bed.CreateRouteAliasAsync("manuals");
        var other = await _bed.CreateRouteAliasAsync("docs", "#/news");

        Assert.IsType<RouteAliasSaveResult.AliasTaken>(
            await _bed.WithRouteAliasCommandsAsync(commands => commands.Update(other, Draft("manuals"))));
    }

    [Fact]
    public async Task Update_UnknownRouteAlias_IsNotFound()
    {
        Assert.IsType<RouteAliasSaveResult.NotFound>(
            await _bed.WithRouteAliasCommandsAsync(commands => commands.Update(Guid.NewGuid(), Draft())));
    }

    [Fact]
    public async Task Update_DeletedRouteAlias_IsNotFound()
    {
        var key = await _bed.CreateRouteAliasAsync();
        await _bed.WithRouteAliasCommandsAsync(commands => commands.Delete(key));

        Assert.IsType<RouteAliasSaveResult.NotFound>(
            await _bed.WithRouteAliasCommandsAsync(commands => commands.Update(key, Draft())));
    }

    [Fact]
    public async Task Delete_SoftDeletesEveryVersionAndFreesTheAlias()
    {
        var key = await _bed.CreateRouteAliasAsync("manuals");
        await _bed.WithRouteAliasCommandsAsync(commands => commands.Update(key, Draft("manuals", "#/news")));

        var removed = await _bed.WithRouteAliasCommandsAsync(commands => commands.Delete(key));

        Assert.True(removed);
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("RouteAlias", key));
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("RouteAlias", key, 2));
        Assert.IsType<RouteAliasSaveResult.Saved>(
            await _bed.WithRouteAliasCommandsAsync(commands => commands.Create(Draft("manuals", "#/articles"))));
    }

    [Fact]
    public async Task Delete_UnknownRouteAlias_IsNotFound()
    {
        Assert.False(await _bed.WithRouteAliasCommandsAsync(commands => commands.Delete(Guid.NewGuid())));
    }

    [Fact]
    public async Task Restore_BringsTheRouteAliasBack()
    {
        var key = await _bed.CreateRouteAliasAsync();
        await _bed.WithRouteAliasCommandsAsync(commands => commands.Delete(key));

        var result = await _bed.WithRouteAliasCommandsAsync(commands => commands.Restore(key));

        Assert.Equal(new VersionedKey<Guid>(key, 1), Assert.IsType<RouteAliasSaveResult.Saved>(result).Id);
        Assert.Equal(["Created", "Deleted", "Restored"], await _bed.ActionsAsync("RouteAlias", key));
    }

    [Fact]
    public async Task Restore_RouteAliasThatIsNotDeleted_IsNotFound()
    {
        var key = await _bed.CreateRouteAliasAsync();

        Assert.IsType<RouteAliasSaveResult.NotFound>(
            await _bed.WithRouteAliasCommandsAsync(commands => commands.Restore(key)));
    }

    [Fact]
    public async Task Restore_WhileAnotherRouteAliasUsesTheAlias_IsRejected()
    {
        var key = await _bed.CreateRouteAliasAsync("manuals");
        await _bed.WithRouteAliasCommandsAsync(commands => commands.Delete(key));
        await _bed.CreateRouteAliasAsync("manuals", "#/news");

        Assert.IsType<RouteAliasSaveResult.AliasTaken>(
            await _bed.WithRouteAliasCommandsAsync(commands => commands.Restore(key)));
    }

    [Fact]
    public async Task HardDelete_RemovesADeletedRouteAliasForGood()
    {
        var key = await _bed.CreateRouteAliasAsync();
        await _bed.WithRouteAliasCommandsAsync(commands => commands.Delete(key));

        var result = await _bed.WithRouteAliasCommandsAsync(commands => commands.HardDelete(key));

        Assert.Equal(HardDeleteResult.Deleted, result);
        await using var ctx = _bed.Read();
        using var scope = ctx.SoftDelete.IncludeDeleted();
        Assert.False(await ctx.RouteAliases.OfKey(key).AnyAsync());
    }

    [Fact]
    public async Task HardDelete_RouteAliasThatIsNotDeleted_IsNotFound()
    {
        var key = await _bed.CreateRouteAliasAsync();

        Assert.Equal(
            HardDeleteResult.NotFound,
            await _bed.WithRouteAliasCommandsAsync(commands => commands.HardDelete(key)));
    }
}
