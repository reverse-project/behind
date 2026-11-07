using Behind.Common.Database.Modules;

namespace Behind.Quotes.Database;

public sealed class QuotesSeed(IQuoteCommands commands) : IModuleSeed
{
    public static readonly Guid WelcomeQuoteId = Guid.Parse("a2000000-0000-0000-0000-0000000000a1");

    public static readonly IQuoteCommands.QuoteDraft WelcomeQuote =
        new("A simple module is a module that is easy to copy.", "Behind");

    public async Task SeedAsync(CancellationToken cancellationToken = default) =>
        await commands.Ensure(WelcomeQuoteId, WelcomeQuote, cancellationToken);
}
