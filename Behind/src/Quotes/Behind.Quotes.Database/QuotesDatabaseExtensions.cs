using Behind.Common.Database.Modules;
using Behind.Quotes.Database.Commands;
using Behind.Quotes.Database.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Quotes.Database;

public static class QuotesDatabaseExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddQuotesDatabase() =>
            services
                .AddModuleContext<IQuotesContext>()
                .AddScoped<IQuoteCommands, QuoteCommands>()
                .AddScoped<IQuoteQueries, QuoteQueries>()
                .AddScoped<IAuditLabelSource, QuotesAuditLabelSource>()
                .AddScoped<IModuleSeed, QuotesSeed>();
    }
}
