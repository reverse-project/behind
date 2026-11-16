using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Behind.Core.Database.Postgres;

public sealed class CoreContextPostgresFactory : IDesignTimeDbContextFactory<CoreContextPostgres>
{
    public CoreContextPostgres CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CoreContextPostgres>()
            .UseNpgsql(DesignTimeConnection.ConnectionString)
            .Options);
}

public sealed class VersioningContextPostgresFactory : IDesignTimeDbContextFactory<VersioningContextPostgres>
{
    public VersioningContextPostgres CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<VersioningContextPostgres>();
        builder.UseVersioningPostgres(DesignTimeConnection.ConnectionString);

        return new VersioningContextPostgres(builder.Options);
    }
}

internal static class DesignTimeConnection
{
    public const string ConnectionString = "Host=localhost;Database=behind";
}
