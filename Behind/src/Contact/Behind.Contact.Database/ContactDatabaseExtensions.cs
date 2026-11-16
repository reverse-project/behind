using Behind.Common.Database.Modules;
using Behind.Contact.Database.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Contact.Database;

public static class ContactDatabaseExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddContactDatabase() => services
            .AddModuleContext<IContactContext>()
            .AddScoped<ContactFormCommands>()
            .AddScoped<ContactRequestCommands>()
            .AddScoped<IAuditLabelSource, ContactAuditLabelSource>();
    }
}
