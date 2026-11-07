using Behind.Common.Database.Modules;
using Behind.Quotes.Database;

namespace Behind.Tests.Quotes;

public sealed class QuotesAuditLabelSourceTests : IDisposable
{
    private readonly QuotesTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<IReadOnlyList<AuditLabel>> LabelsAsync(params Guid[] ids)
    {
        await using var context = _bed.Read();

        return await new QuotesAuditLabelSource(context).LabelsAsync(ids);
    }

    [Fact]
    public async Task Labels_UseTheTextOfTheQuote()
    {
        var key = await _bed.CreateAsync("Keep it simple", "Ada");

        Assert.Equal([new AuditLabel(key, "Keep it simple")], await LabelsAsync(key));
    }

    [Fact]
    public async Task Labels_UseTheLatestVersion()
    {
        var key = await _bed.CreateAsync("Old", "Ada");
        await _bed.WithCommandsAsync(commands => commands.Update(key, new IQuoteCommands.QuoteDraft("New", "Ada")));

        Assert.Equal([new AuditLabel(key, "New")], await LabelsAsync(key));
    }

    [Fact]
    public async Task Labels_StillNameDeletedQuotes()
    {
        var key = await _bed.CreateAsync("Gone", "Ada");
        await _bed.WithCommandsAsync(commands => commands.Delete(key));

        Assert.Equal([new AuditLabel(key, "Gone")], await LabelsAsync(key));
    }

    [Fact]
    public async Task Labels_ShortenLongTexts()
    {
        var key = await _bed.CreateAsync(new string('a', 200), "Ada");

        var label = Assert.Single(await LabelsAsync(key)).Label;

        Assert.Equal(80, label.Length);
        Assert.EndsWith("…", label);
    }

    [Fact]
    public async Task Labels_IgnoreUnknownIdsAndOtherQuotes()
    {
        var key = await _bed.CreateAsync("Mine", "Ada");
        await _bed.CreateAsync("Other", "Bob");

        Assert.Equal([new AuditLabel(key, "Mine")], await LabelsAsync(key, Guid.NewGuid()));
        Assert.Empty(await LabelsAsync(Guid.NewGuid()));
    }
}
