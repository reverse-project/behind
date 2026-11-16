using Behind.Core.Common.Configuration;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Database.Sqlite;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using OtpNet;

namespace Behind.Tests.Helpers;

public sealed class TwoFactorTestBed : IDisposable
{
    public const string OriginText = "https://example.test";

    public static readonly Uri Origin = new($"{OriginText}/");

    public static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    public TwoFactorTestBed(bool required = false, AccountsConfiguration? accounts = null)
    {
        Time = new AdjustableTimeProvider(Start);
        Context = Factory.CreateContext();
        Security = Options.Create(new SecurityConfiguration { TwoFactorRequired = required });
        Queries = new TwoFactorQueries(Context);
        Commands = new TwoFactorCommands(Context);
        Tickets = new TwoFactorTickets(DataProtection, Time);
        Ceremonies = new PasskeyCeremonies(NullLogger<PasskeyCeremonies>.Instance);
        RecoveryCodes = new RecoveryCodes(Queries, Commands);
        Verification = new TwoFactorVerification(Queries, Commands, RecoveryCodes, Ceremonies, Time);
        Enrollment = new TwoFactorEnrollment(Queries, Commands, RecoveryCodes, Ceremonies, Tickets, Security, Time);
        Gate = new TwoFactorGate(Queries, new EmailQueries(Context), new MagicLinkCommands(Context), Time, Tickets, Security,
            FixedOptionsSnapshot.Of(accounts ?? new AccountsConfiguration()));
    }

    public TestDbContextFactory Factory { get; } = new();

    public IDataProtectionProvider DataProtection { get; } = DataProtectionProvider.Create("two-factor-tests");

    public AdjustableTimeProvider Time { get; }

    public CoreContextSqlite Context { get; }

    public IOptions<SecurityConfiguration> Security { get; }

    public TwoFactorQueries Queries { get; }

    public TwoFactorCommands Commands { get; }

    public TwoFactorTickets Tickets { get; }

    public PasskeyCeremonies Ceremonies { get; }

    public RecoveryCodes RecoveryCodes { get; }

    public TwoFactorVerification Verification { get; }

    public TwoFactorEnrollment Enrollment { get; }

    public TwoFactorGate Gate { get; }

    public void Dispose()
    {
        Context.Dispose();
        Factory.Dispose();
    }

    public async Task<ApplicationUser> SeedUser(string email = "ada@test.com")
    {
        await using var seed = Factory.CreateContext();
        var id = await DatabaseSeedHelper.SeedHumanUserAsync(seed, email);
        await new EmailCommands(seed, Time).ConfirmPrimary(id);

        return new ApplicationUser { Id = id, DisplayName = "Ada", Emails = [new Email(email, Start)] };
    }

    public async Task<string> PendingSecret(Guid userId) =>
        (await Queries.FindTotp(userId)).Match(() => throw new InvalidOperationException(), totp => totp.Secret);

    public string CodeFor(string secret, TimeSpan? offset = null) =>
        new OtpNet.Totp(Base32Encoding.ToBytes(secret))
            .ComputeTotp((Time.GetUtcNow() + (offset ?? TimeSpan.Zero)).UtcDateTime);

    public async Task EnableTotp(ApplicationUser user)
    {
        await Enrollment.BeginTotp(user);
        await Enrollment.ConfirmTotp(user.Id, CodeFor(await PendingSecret(user.Id)));
        Time.Advance(TimeSpan.FromMinutes(1));
    }

    public async Task<VirtualAuthenticator> EnablePasskey(ApplicationUser user, string? name = null)
    {
        var authenticator = new VirtualAuthenticator();
        var challenge = await Enrollment.BeginPasskey(Origin, user, TwoFactorStage.Profile, null);
        var ticket = Tickets.Open(challenge.Ticket).Match(() => throw new InvalidOperationException(), found => found);

        await Enrollment.CompletePasskey(Origin, ticket, name, authenticator.Register(challenge.Options, OriginText));

        return authenticator;
    }
}
