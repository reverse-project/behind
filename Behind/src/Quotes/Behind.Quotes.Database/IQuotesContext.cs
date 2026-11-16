using Behind.Common.Database.Modules;
using Behind.Quotes.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Quotes.Database;

public interface IQuotesContext : IModuleContext
{
    DbSet<Quote> Quotes { get; }
}
