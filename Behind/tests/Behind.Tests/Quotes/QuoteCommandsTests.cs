using Behind.Quotes.Database;
using Behind.Quotes.Database.Commands;
using Behind.Quotes.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.Quotes;

public sealed class QuoteCommandsTests : IDisposable
{
    private readonly QuotesTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private static IQuoteCommands.QuoteDraft Draft(string text = "Keep it simple", string author = "Ada") =>
        new(text, author);

    private async Task<IReadOnlyList<(long Version, string Text, string Author)>> VersionsAsync(Guid key)
    {
        await using var context = _bed.Read();
        using var scope = context.SoftDelete.IncludeDeleted();
        var rows = await context.Quotes
            .OfKey(key)
            .OrderBy(quote => EF.Property<long>(quote, VersionedColumns.Version))
            .Select(quote => new { Version = EF.Property<long>(quote, VersionedColumns.Version), quote.Text, quote.Author })
            .ToListAsync();

        return rows.Select(row => (row.Version, row.Text, row.Author)).ToList();
    }

    [Fact]
    public async Task Create_StoresVersionOneAndAuditsTheCreation()
    {
        var key = await _bed.CreateAsync("Keep it simple", "Ada");

        Assert.Equal([(1L, "Keep it simple", "Ada")], await VersionsAsync(key));
        Assert.Equal(["Created"], await _bed.ActionsAsync(key));
    }

    [Fact]
    public async Task Update_InsertsTheNextVersionAndKeepsTheOldOne()
    {
        var key = await _bed.CreateAsync("Keep it simple", "Ada");

        var result = await _bed.WithCommandsAsync(commands => commands.Update(key, Draft("Keep it small", "Grace")));

        Assert.Equal(new VersionedKey<Guid>(key, 2), Assert.IsType<QuoteSaveResult.Saved>(result).Id);
        Assert.Equal([(1L, "Keep it simple", "Ada"), (2L, "Keep it small", "Grace")], await VersionsAsync(key));
    }

    [Fact]
    public async Task Update_OfAnUnknownQuote_IsNotFound()
    {
        var result = await _bed.WithCommandsAsync(commands => commands.Update(Guid.NewGuid(), Draft()));

        Assert.IsType<QuoteSaveResult.NotFound>(result);
    }

    [Fact]
    public async Task Update_OfADeletedQuote_IsNotFound()
    {
        var key = await _bed.CreateAsync();
        await _bed.WithCommandsAsync(commands => commands.Delete(key));

        var result = await _bed.WithCommandsAsync(commands => commands.Update(key, Draft("Other", "Other")));

        Assert.IsType<QuoteSaveResult.NotFound>(result);
    }

    [Fact]
    public async Task Delete_SoftDeletesTheQuoteAndKeepsItsRows()
    {
        var key = await _bed.CreateAsync();

        Assert.True(await _bed.WithCommandsAsync(commands => commands.Delete(key)));

        Assert.Empty(await _bed.ListAsync());
        Assert.Single(await _bed.ListAsync(DeletionState.Deleted));
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync(key));
        Assert.Single(await VersionsAsync(key));
    }

    [Fact]
    public async Task Delete_OfAnUnknownQuote_ReportsNotFound()
    {
        Assert.False(await _bed.WithCommandsAsync(commands => commands.Delete(Guid.NewGuid())));
    }

    [Fact]
    public async Task Restore_BringsADeletedQuoteBack()
    {
        var key = await _bed.CreateAsync();
        await _bed.WithCommandsAsync(commands => commands.Delete(key));

        Assert.True(await _bed.WithCommandsAsync(commands => commands.Restore(key)));

        Assert.Single(await _bed.ListAsync());
        Assert.Empty(await _bed.ListAsync(DeletionState.Deleted));
        Assert.Equal(["Created", "Deleted", "Restored"], await _bed.ActionsAsync(key));
    }

    [Fact]
    public async Task Restore_OfAnActiveQuote_ReportsNotFound()
    {
        var key = await _bed.CreateAsync();

        Assert.False(await _bed.WithCommandsAsync(commands => commands.Restore(key)));
    }

    [Fact]
    public async Task HardDelete_RemovesADeletedQuoteForGood()
    {
        var key = await _bed.CreateAsync();
        await _bed.WithCommandsAsync(commands => commands.Delete(key));

        var result = await _bed.WithCommandsAsync(commands => commands.HardDelete(key));

        Assert.Equal(HardDeleteResult.Deleted, result);
        Assert.Empty(await VersionsAsync(key));
        Assert.Empty(await _bed.ListAsync(DeletionState.Deleted));
    }

    [Fact]
    public async Task HardDelete_OfAnActiveOrUnknownQuote_IsNotFound()
    {
        var key = await _bed.CreateAsync();

        Assert.Equal(HardDeleteResult.NotFound, await _bed.WithCommandsAsync(commands => commands.HardDelete(key)));
        Assert.Equal(
            HardDeleteResult.NotFound,
            await _bed.WithCommandsAsync(commands => commands.HardDelete(Guid.NewGuid())));
        Assert.Single(await _bed.ListAsync());
    }

    [Fact]
    public async Task Ensure_CreatesTheQuoteOnlyOnce()
    {
        var key = Guid.NewGuid();

        Assert.True(await _bed.WithCommandsAsync(commands => commands.Ensure(key, Draft())));
        Assert.False(await _bed.WithCommandsAsync(commands => commands.Ensure(key, Draft("Changed", "Other"))));

        Assert.Equal([(1L, "Keep it simple", "Ada")], await VersionsAsync(key));
    }

    [Fact]
    public async Task Ensure_DoesNotRecreateADeletedQuote()
    {
        var key = Guid.NewGuid();
        await _bed.WithCommandsAsync(commands => commands.Ensure(key, Draft()));
        await _bed.WithCommandsAsync(commands => commands.Delete(key));

        Assert.False(await _bed.WithCommandsAsync(commands => commands.Ensure(key, Draft())));

        Assert.Empty(await _bed.ListAsync());
    }

    [Fact]
    public async Task Create_WhenTheVersionCannotBeStored_ReportsAVersionConflict()
    {
        await using var context = _bed.WriteFailing();

        var result = await new QuoteCommands(context).Create(Draft());

        Assert.IsType<QuoteSaveResult.VersionConflict>(result);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Update_WhenTheVersionCannotBeStored_ReportsAVersionConflict()
    {
        var key = await _bed.CreateAsync();
        await using var context = _bed.WriteFailing();

        var result = await new QuoteCommands(context).Update(key, Draft("Other", "Other"));

        Assert.IsType<QuoteSaveResult.VersionConflict>(result);
        Assert.Equal([(1L, "Keep it simple", "Ada")], await VersionsAsync(key));
    }

    [Fact]
    public async Task HardDelete_WhenTheDatabaseRejectsIt_IsRejected()
    {
        var key = await _bed.CreateAsync();
        await _bed.WithCommandsAsync(commands => commands.Delete(key));
        await using var context = _bed.WriteFailing();

        var result = await new QuoteCommands(context).HardDelete(key);

        Assert.Equal(HardDeleteResult.Rejected, result);
        Assert.Single(await VersionsAsync(key));
    }
}
