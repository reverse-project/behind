using Behind.Common.Database.Modules;
using Behind.Contact.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Contact.Database;

public interface IContactContext : IModuleContext
{
    DbSet<ContactForm> ContactForms { get; }
    DbSet<ContactRequest> ContactRequests { get; }
}
