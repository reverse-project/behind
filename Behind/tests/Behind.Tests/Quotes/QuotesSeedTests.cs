using Behind.Quotes.Database;
using Behind.Quotes.Database.Commands;

namespace Behind.Tests.Quotes;

public sealed class QuotesSeedTests : IDisposable
{
    private readonly QuotesTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task SeedAsync()
    {
        await using var context = _bed.Write();

        await new QuotesSeed(new QuoteCommands(context)).SeedAsync();
    }

    [Fact]
    public async Task Seed_AddsTheWelcomeQuote()
    {
        await SeedAsync();

        var quote = Assert.Single(await _bed.ListAsync());
        Assert.Equal(QuotesSeed.WelcomeQuoteId, quote.Id.Key);
        Assert.Equal((QuotesSeed.WelcomeQuote.Text, QuotesSeed.WelcomeQuote.Author), (quote.Text, quote.Author));
    }

    [Fact]
    public async Task Seed_RunTwice_KeepsASingleQuote()
    {
        await SeedAsync();
        await SeedAsync();

        Assert.Single(await _bed.ListAsync());
    }

    [Fact]
    public async Task Seed_DoesNotBringBackADeletedWelcomeQuote()
    {
        await SeedAsync();
        await _bed.WithCommandsAsync(commands => commands.Delete(QuotesSeed.WelcomeQuoteId));

        await SeedAsync();

        Assert.Empty(await _bed.ListAsync());
    }
}
