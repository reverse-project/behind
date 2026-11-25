using Behind.Core.Database.Auditing;
using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Behind.Quotes.Database;
using Behind.Quotes.Database.Commands;
using Behind.Quotes.Domain;
using Behind.Quotes.WebApi;
using Behind.Web.Database.Postgres;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Postgres;
using Behind.Core.Database.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Behind.Tests.CoreDatabase;

[Trait("Category", "Integration")]
public sealed class DatabaseSecurityPostgresIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IssuedBansSurviveIssuerDeletionAfterFreshInstallOrUpgrade(bool upgrade)
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"] ?? throw new InvalidOperationException("Postgres image required.");
        await using var postgres = new PostgreSqlBuilder(image).Build();
        await postgres.StartAsync();
        await using var context = new CoreContextPostgres(new DbContextOptionsBuilder<CoreContextPostgres>()
            .UseNpgsql(postgres.GetConnectionString())
            .AddInterceptors(Auditing()).Options);
        if (upgrade)
        {
            await context.GetService<IMigrator>().MigrateAsync("20261117120000_RoleNameUniqueness");
        }
        else
        {
            await context.Database.MigrateAsync();
        }
        var issuer = await PostgresSecuritySeed.User(context, "issuer@test.com");
        var target = await PostgresSecuritySeed.User(context, "banned@test.com");
        await new WallOfShameCommands(context).Ban(target, issuer, new Reason("Misconduct"));
        await context.Database.MigrateAsync();
        await context.Database.MigrateAsync();
        await new UserCommands(context).Delete(issuer);
        Assert.Single(await context.WallOfShameEntries.ToListAsync());
        Assert.Equal(IUserCommands.HardDeleteOutcome.Rejected, await new UserCommands(context).HardDelete(issuer));
    }

    [Fact]
    public async Task HardDeletePrimaryRequiresUnlinkedSubaccounts()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"] ?? throw new InvalidOperationException("Postgres image required.");
        await using var postgres = new PostgreSqlBuilder(image).Build();
        await postgres.StartAsync();
        await using var context = new CoreContextPostgres(new DbContextOptionsBuilder<CoreContextPostgres>()
            .UseNpgsql(postgres.GetConnectionString())
            .AddInterceptors(Auditing()).Options);
        await context.Database.MigrateAsync();

        var primaryId = await PostgresSecuritySeed.User(context, "primary@test.com");
        var childId = await PostgresSecuritySeed.User(context, "child@test.com");
        var primary = (await context.Users.FindAsync(primaryId))!;
        var child = (await context.Users.FindAsync(childId))!;
        context.Subaccounts.Add(new Subaccount(primary, child, true));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var commands = new UserCommands(context);
        await commands.Delete(primaryId);
        context.ChangeTracker.Clear();

        Assert.Equal(IUserCommands.HardDeleteOutcome.LinkedSubaccounts, await commands.HardDelete(primaryId));
        Assert.True(await context.Users.IgnoreQueryFilters().AnyAsync(user => EF.Property<Guid>(user, "Id") == primaryId));
        Assert.True(await context.Users.AnyAsync(user => EF.Property<Guid>(user, "Id") == childId));

        context.AppendSubaccountVersion(await context.ActiveSubaccounts().SingleAsync(), isActive: false);
        await context.SaveChangesAsync();

        Assert.Equal(IUserCommands.HardDeleteOutcome.Deleted, await commands.HardDelete(primaryId));
        Assert.False(await context.Users.IgnoreQueryFilters().AnyAsync(user => EF.Property<Guid>(user, "Id") == primaryId));
        Assert.True(await context.Users.AnyAsync(user => EF.Property<Guid>(user, "Id") == childId));
    }

    [Fact]
    public async Task TotpProofAndRefreshRotationUseConditionalWrites()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"] ?? throw new InvalidOperationException("Postgres image required.");
        await using var postgres = new PostgreSqlBuilder(image).Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql(postgres.GetConnectionString())
            .AddInterceptors(Auditing()).Options;
        await using var context = new CoreContextPostgres(options);
        await context.Database.MigrateAsync();
        var userId = await PostgresSecuritySeed.User(context, "owner@test.com");
        var commands = new TwoFactorCommands(context);
        await commands.SaveTotp(userId, "FIRST");
        await commands.SaveTotp(userId, "SECOND");
        Assert.False(await commands.ConfirmTotp(userId, "FIRST", 42));
        Assert.True(await commands.ConfirmTotp(userId, "SECOND", 42));
        Assert.False(await commands.ConfirmTotp(userId, "SECOND", 43));
        var totpActions = await context.AuditLogEntries.Where(entry => entry.EntityName == nameof(UserTotp))
            .Select(entry => entry.Action).ToListAsync();
        var confirmation = Assert.Single(totpActions.OfType<AuditLogEntry.Updated>());
        Assert.Equal(new[] { nameof(UserTotp.IsConfirmed), nameof(UserTotp.LastUsedStep) },
            confirmation.Differences.Select(change => change.PropertyName).Order());
        Assert.False(await commands.AdvanceTotpStep(userId, "FIRST", 43));
        await commands.ReplaceRecoveryCodes(userId, ["first"]);
        await commands.ReplaceRecoveryCodes(userId, ["second"]);
        Assert.Equal("second", Assert.Single(await new TwoFactorQueries(context).GetRecoveryCodes(userId)).Hash);
        var guest = new GuestUser(None);
        var session = new ValidSession(AccessToken.Create(guest), RefreshToken.Create(guest), "127.0.0.1", "test");
        var sessionId = await new SessionCommands(context).CreateForUserId(userId, session);
        async Task<bool> Rotate()
        {
            await using var competing = new CoreContextPostgres(options);
            return await new SessionCommands(competing).RenewTokens(sessionId, session.AccessToken.Nonce,
                session.RefreshToken.Nonce, AccessToken.Create(guest), RefreshToken.Create(guest));
        }
        var results = await Task.WhenAll(Rotate(), Rotate());
        Assert.Single(results.Where(result => result));
        var sessionActions = await context.AuditLogEntries.Where(entry => entry.EntityName == nameof(ValidSession)
                && entry.EntityKey == sessionId.ToString())
            .Select(entry => entry.Action).ToListAsync();
        var renewal = Assert.Single(sessionActions.OfType<AuditLogEntry.Updated>());
        Assert.Equal(6, renewal.Differences.Count());
        var nonces = renewal.Differences.Where(change => change.PropertyName is "AccessToken.Nonce" or "RefreshToken.Nonce").ToList();
        Assert.Equal(2, nonces.Count);
        Assert.All(nonces, change =>
        {
            Assert.Equal(EntityEntryAuditExtensions.MaskedValue, change.OldValue?.ToString());
            Assert.Equal(EntityEntryAuditExtensions.MaskedValue, change.NewValue?.ToString());
        });
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletionBetweenLookupAndSaveCannotRestoreContent(bool generic)
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"] ?? throw new InvalidOperationException("Postgres image required.");
        await using var postgres = new PostgreSqlBuilder(image).Build();
        await postgres.StartAsync();
        var catalog = ModuleCatalog.Create([QuotesModule.Descriptor, Behind.Sport.WebApi.SportModule.Descriptor, Behind.Forum.WebApi.ForumModule.Descriptor]).GetOrElse(ModuleCatalog.Empty);
        var options = new DbContextOptionsBuilder<WebContextPostgres>().UseNpgsql(postgres.GetConnectionString())
            .AddInterceptors(Auditing()).Options;
        await using var seed = new WebContextPostgres(options, catalog);
        await seed.Database.EnsureCreatedAsync();
        var created = Assert.IsType<QuoteSaveResult.Saved>(await new QuoteCommands(seed).Create(new("original", "author")));
        var id = created.Id.Key;
        var deleted = false;
        var interceptor = new BeforeInsert(async () =>
        {
            await using var deleting = new WebContextPostgres(options, catalog);
            Assert.True(await new QuoteCommands(deleting).Delete(id));
            deleted = true;
        });
        var updatingOptions = new DbContextOptionsBuilder<WebContextPostgres>().UseNpgsql(postgres.GetConnectionString())
            .AddInterceptors(Auditing(), interceptor).Options;
        await using var updating = new WebContextPostgres(updatingOptions, catalog);
        var error = await Record.ExceptionAsync(async () =>
        {
            if (generic)
            {
                await new GenericQuotes(updating).Update(id, 1, new("replacement", "author"));
            }
            else
            {
                await new QuoteCommands(updating).Update(id, new("replacement", "author"));
            }
        });
        Assert.True(deleted);
        if (error is not null)
        {
            Assert.True(error is System.Data.Common.DbException or DbUpdateException, error.ToString());
        }
        await using var reader = new WebContextPostgres(options, catalog);
        Assert.Equal(0, await reader.Quotes.LatestVersionAsync(id));
    }

    private static AuditSaveChangesInterceptor Auditing() =>
        new(() => new FixedAuditActorProvider(AuditActor.Anonymous), TimeProvider.System);

    private sealed class GenericQuotes(IModuleContext context) : VersionedCommands<Quote, IQuoteCommands.QuoteDraft>(context)
    {
        protected override Quote Build(IQuoteCommands.QuoteDraft draft) => new(draft.Text, draft.Author);
    }

    private sealed class BeforeInsert(Func<Task> action) : SaveChangesInterceptor
    {
        private bool _called;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_called && eventData.Context!.ChangeTracker.Entries<Quote>().Any(entry => entry.State == EntityState.Added))
            {
                _called = true;
                await action();
            }
            return result;
        }
    }

}

internal static class PostgresSecuritySeed
{
    public static async Task<Guid> User(AbstractCoreContext context, string email)
    {
        var user = new HumanUser("Test User", [new Email(email)], None);
        await context.Users.AddAsync(user);
        var id = context.Entry(user).Property<Guid>("Id").CurrentValue;
        await context.SaveChangesAsync();
        return id;
    }
}
