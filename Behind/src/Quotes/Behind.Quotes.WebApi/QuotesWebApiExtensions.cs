using Behind.Common.Modules;
using Behind.Common.WebApi.Middleware;
using Behind.Quotes.Database;
using Behind.Quotes.WebApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Quotes.WebApi;

public static class QuotesWebApiExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddQuotes()
        {
            builder.Services
                .AddModule(QuotesModule.Descriptor)
                .AddQuotesDatabase();

            return builder;
        }
    }

    extension(WebApplication application)
    {
        public WebApplication UseQuotes()
        {
            application.MapGroup("").WithMetadata(new ModuleEndpoint(QuotesModule.Descriptor.Key))
                .MapQuoteEndpoints();

            return application;
        }
    }
}
