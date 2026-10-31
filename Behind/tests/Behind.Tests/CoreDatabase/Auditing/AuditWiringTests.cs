using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Queries;
using Behind.Core.Database.Sqlite;
using Behind.Core.WebApi.Auditing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqliteProvider = Behind.Core.Common.Configuration.Sqlite;

namespace Behind.Tests.CoreDatabase.Auditing;

public sealed class AuditWiringTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"behind-audit-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }

    private ServiceProvider BuildServices(bool withHttpActor)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["ConnectionStrings:Test"] = $"Data Source={_databasePath}";

        new DatabaseConfiguration(Some<Provider>(new SqliteProvider()), Some(new Key("Test"))).RegisterSqlite(builder);

        if (withHttpActor)
        {
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ISessionQueries, SessionQueries>();
            builder.Services.AddScoped<IAuditActorProvider, HttpAuditActorProvider>();
        }

        return builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegisteredContext_AuditsEveryChange_WithoutCircularDependencies(bool withHttpActor)
    {
        await using var services = BuildServices(withHttpActor);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AbstractCoreContext>();
        await context.Database.EnsureCreatedAsync();
        await context.EnsureBuiltInUsersAsync();

        var grant = new Grant("wired-grant");
        await context.Grants.AddAsync(grant);
        var key = context.Entry(grant).Property<Guid>("Id").CurrentValue;
        await context.SaveChangesAsync();

        var audit = await context.AuditLogEntries
            .Where(a => a.EntityName == nameof(Grant) && a.EntityKey == key.ToString())
            .Select(a => new { a.Action, UserId = EF.Property<Guid?>(a, "UserId") })
            .SingleAsync();

        Assert.IsType<AuditLogEntry.Created>(audit.Action);
        Assert.Equal(DomainConstants.SystemUserId, audit.UserId);
    }

    [Fact]
    public void RegisteredServices_DefaultToAutonomousActor()
    {
        using var services = BuildServices(false);
        using var scope = services.CreateScope();

        Assert.IsType<AutonomousAuditActorProvider>(scope.ServiceProvider.GetRequiredService<IAuditActorProvider>());
    }
}
