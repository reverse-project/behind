using Behind.Common.Modules;
using Behind.Common.WebApi;
using Behind.Contact.Database;
using Behind.Contact.WebApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Contact.WebApi;

public static class ContactWebApiExtensions
{
    public const string SubmissionPolicy = "contact-submit";

    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddContact()
        {
            builder.Services.AddModule(ContactModule.Descriptor).AddContactDatabase();
            builder.Services.AddRateLimiter(options => options.AddClientPolicy(SubmissionPolicy, 5, TimeSpan.FromMinutes(10)));
            return builder;
        }
    }

    extension(WebApplication application)
    {
        public WebApplication UseContact()
        {
            application.MapContactEndpoints();
            return application;
        }
    }
}
