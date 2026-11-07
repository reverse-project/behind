using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public sealed record DatabaseProviderOptions(
    Func<DbContextOptionsBuilder, string, DbContextOptionsBuilder> UseCore,
    Func<DbContextOptionsBuilder, string, DbContextOptionsBuilder> UseVersioning,
    Func<DbContextOptionsBuilder, string, string, DbContextOptionsBuilder> UseModule);
