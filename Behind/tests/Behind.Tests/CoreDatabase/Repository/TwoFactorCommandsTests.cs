using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase.Repository;

public sealed class TwoFactorCommandsTests : IDisposable
{
    private static readonly DateTimeOffset Created = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<Guid> SeedUser(string email = "owner@test.com")
    {
        await using var seed = _factory.CreateContext();

        return await DatabaseSeedHelper.SeedHumanUserAsync(seed, email);
    }

    private static UserPasskey Passkey(string name = "Laptop", string credentialId = "credential-1") =>
        new(name, credentialId, "public-key");

    [Fact]
    public async Task GetStatus_UserWithoutTwoFactor_IsDisabled()
    {
        var userId = await SeedUser();

        await using var ctx = _factory.CreateContext();
        var status = await new TwoFactorQueries(ctx).GetStatus(userId);

        Assert.False(status.IsEnabled);
        Assert.False(status.TotpEnabled);
        Assert.Empty(status.Passkeys);
        Assert.Equal(0, status.RecoveryCodesRemaining);
    }

    [Fact]
    public async Task SaveTotp_StoresAnUnconfirmedSecret()
    {
        var userId = await SeedUser();

        await using (var ctx = _factory.CreateContext())
        {
            await new TwoFactorCommands(ctx).SaveTotp(userId, "SECRET");
        }

        await using var read = _factory.CreateContext();
        var queries = new TwoFactorQueries(read);
        var totp = (await queries.FindTotp(userId)).Match<ITwoFactorQueries.TotpInfo>(() => throw new InvalidOperationException(), found => found);

        Assert.Equal("SECRET", totp.Secret);
        Assert.False(totp.IsConfirmed);
        Assert.False((await queries.GetStatus(userId)).IsEnabled);
    }

    [Fact]
    public async Task SaveTotp_AgainReplacesThePreviousSecret()
    {
        var userId = await SeedUser();

        await using (var ctx = _factory.CreateContext())
        {
            await new TwoFactorCommands(ctx).SaveTotp(userId, "FIRST");
        }

        await using (var ctx = _factory.CreateContext())
        {
            await new TwoFactorCommands(ctx).SaveTotp(userId, "SECOND");
        }

        await using var read = _factory.CreateContext();

        Assert.Equal(1, await read.UserTotps.CountAsync());
        Assert.Equal("SECOND", (await new TwoFactorQueries(read).FindTotp(userId)).Match(() => "", totp => totp.Secret));
    }

    [Fact]
    public async Task ConfirmTotp_EnablesTotpAndRemembersTheStep()
    {
        var userId = await SeedUser();

        await using (var ctx = _factory.CreateContext())
        {
            var commands = new TwoFactorCommands(ctx);
            await commands.SaveTotp(userId, "SECRET");
            Assert.True(await commands.ConfirmTotp(userId, 42));
        }

        await using var read = _factory.CreateContext();
        var queries = new TwoFactorQueries(read);
        var totp = (await queries.FindTotp(userId)).Match<ITwoFactorQueries.TotpInfo>(() => throw new InvalidOperationException(), found => found);

        Assert.True(totp.IsConfirmed);
        Assert.Equal(42, totp.LastUsedStep);
        Assert.True((await queries.GetStatus(userId)).TotpEnabled);
    }

    [Fact]
    public async Task ConfirmTotp_WithoutPendingSecretOrWhenAlreadyConfirmed_ReturnsFalse()
    {
        var userId = await SeedUser();

        await using var ctx = _factory.CreateContext();
        var commands = new TwoFactorCommands(ctx);

        Assert.False(await commands.ConfirmTotp(userId, 1));

        await commands.SaveTotp(userId, "SECRET");
        await commands.ConfirmTotp(userId, 1);

        Assert.False(await commands.ConfirmTotp(userId, 2));
    }

    [Fact]
    public async Task AdvanceTotpStep_AcceptsOnlyNewerStepsOfAConfirmedSecret()
    {
        var userId = await SeedUser();

        await using var ctx = _factory.CreateContext();
        var commands = new TwoFactorCommands(ctx);
        await commands.SaveTotp(userId, "SECRET");

        Assert.False(await commands.AdvanceTotpStep(userId, 10));

        await commands.ConfirmTotp(userId, 10);

        Assert.False(await commands.AdvanceTotpStep(userId, 10));
        Assert.False(await commands.AdvanceTotpStep(userId, 9));
        Assert.True(await commands.AdvanceTotpStep(userId, 11));
        Assert.False(await commands.AdvanceTotpStep(userId, 11));
    }

    [Fact]
    public async Task RemoveTotp_DeletesTheSecret()
    {
        var userId = await SeedUser();

        await using var ctx = _factory.CreateContext();
        var commands = new TwoFactorCommands(ctx);
        await commands.SaveTotp(userId, "SECRET");

        Assert.True(await commands.RemoveTotp(userId));
        Assert.False(await commands.RemoveTotp(userId));
        Assert.Equal(None, await new TwoFactorQueries(ctx).FindTotp(userId));
    }

    [Fact]
    public async Task AddPasskey_IsListedForItsUserWithTheAuditCreationTime()
    {
        var userId = await SeedUser();
        var other = await SeedUser("other@test.com");

        await using (var ctx = _factory.CreateAuditedContext(AuditActor.ForUser(userId), new FixedTimeProvider(Created)))
        {
            await new TwoFactorCommands(ctx).AddPasskey(userId, Passkey());
        }

        await using var read = _factory.CreateContext();
        var queries = new TwoFactorQueries(read);
        var passkeys = await queries.GetPasskeys(userId);

        var passkey = Assert.Single(passkeys);
        Assert.Equal("Laptop", passkey.Name);
        Assert.Equal("credential-1", passkey.CredentialId);
        Assert.Equal(Some(Created), passkey.CreatedAt);
        Assert.Empty(await queries.GetPasskeys(other));
        Assert.True((await queries.GetStatus(userId)).IsEnabled);
    }

    [Fact]
    public async Task FindPasskey_FindsOnlyTheCredentialOfTheUser()
    {
        var userId = await SeedUser();
        var other = await SeedUser("other@test.com");

        await using var ctx = _factory.CreateContext();
        await new TwoFactorCommands(ctx).AddPasskey(userId, Passkey());
        var queries = new TwoFactorQueries(ctx);

        Assert.NotEqual(None, await queries.FindPasskey(userId, "credential-1"));
        Assert.Equal(None, await queries.FindPasskey(other, "credential-1"));
        Assert.Equal(None, await queries.FindPasskey(userId, "unknown"));
    }

    [Fact]
    public async Task IsCredentialRegistered_SeesCredentialsOfEveryUser()
    {
        var userId = await SeedUser();

        await using var ctx = _factory.CreateContext();
        await new TwoFactorCommands(ctx).AddPasskey(userId, Passkey());
        var queries = new TwoFactorQueries(ctx);

        Assert.True(await queries.IsCredentialRegistered("credential-1"));
        Assert.False(await queries.IsCredentialRegistered("credential-2"));
    }

    [Fact]
    public async Task UpdatePasskeyCounter_StoresTheNewCounterOfThePasskeyOfTheUser()
    {
        var userId = await SeedUser();
        var other = await SeedUser("other@test.com");

        await using var ctx = _factory.CreateContext();
        var commands = new TwoFactorCommands(ctx);
        await commands.AddPasskey(userId, Passkey());
        var id = (await new TwoFactorQueries(ctx).GetPasskeys(userId)).Single().Id;

        Assert.False(await commands.UpdatePasskeyCounter(other, id, 5));
        Assert.True(await commands.UpdatePasskeyCounter(userId, id, 5));

        await using var read = _factory.CreateContext();

        Assert.Equal(5, (await new TwoFactorQueries(read).GetPasskeys(userId)).Single().SignCount);
    }

    [Fact]
    public async Task RemovePasskey_RemovesOnlyThePasskeyOfTheUser()
    {
        var userId = await SeedUser();
        var other = await SeedUser("other@test.com");

        await using var ctx = _factory.CreateContext();
        var commands = new TwoFactorCommands(ctx);
        await commands.AddPasskey(userId, Passkey());
        await commands.AddPasskey(userId, Passkey("Phone", "credential-2"));
        var queries = new TwoFactorQueries(ctx);
        var laptop = (await queries.GetPasskeys(userId)).Single(passkey => passkey.Name == "Laptop").Id;

        Assert.False(await commands.RemovePasskey(other, laptop));
        Assert.True(await commands.RemovePasskey(userId, laptop));
        Assert.False(await commands.RemovePasskey(userId, laptop));
        Assert.Equal("Phone", Assert.Single(await queries.GetPasskeys(userId)).Name);
    }

    [Fact]
    public async Task ReplaceRecoveryCodes_KeepsOnlyTheNewCodes()
    {
        var userId = await SeedUser();

        await using var ctx = _factory.CreateContext();
        var commands = new TwoFactorCommands(ctx);
        var queries = new TwoFactorQueries(ctx);

        await commands.ReplaceRecoveryCodes(userId, ["a", "b", "c"]);
        await commands.ReplaceRecoveryCodes(userId, ["x", "y"]);

        Assert.Equal(["x", "y"], (await queries.GetRecoveryCodes(userId)).Select(code => code.Hash).Order());
        Assert.Equal(2, (await queries.GetStatus(userId)).RecoveryCodesRemaining);
    }

    [Fact]
    public async Task ConsumeRecoveryCode_RemovesExactlyThatCode()
    {
        var userId = await SeedUser();
        var other = await SeedUser("other@test.com");

        await using var ctx = _factory.CreateContext();
        var commands = new TwoFactorCommands(ctx);
        var queries = new TwoFactorQueries(ctx);
        await commands.ReplaceRecoveryCodes(userId, ["a", "b"]);
        var first = (await queries.GetRecoveryCodes(userId)).Single(code => code.Hash == "a").Id;

        Assert.False(await commands.ConsumeRecoveryCode(other, first));
        Assert.True(await commands.ConsumeRecoveryCode(userId, first));
        Assert.False(await commands.ConsumeRecoveryCode(userId, first));
        Assert.Equal("b", Assert.Single(await queries.GetRecoveryCodes(userId)).Hash);
    }

    [Fact]
    public async Task RemoveRecoveryCodes_RemovesEveryCodeOfTheUser()
    {
        var userId = await SeedUser();

        await using var ctx = _factory.CreateContext();
        var commands = new TwoFactorCommands(ctx);
        await commands.ReplaceRecoveryCodes(userId, ["a", "b"]);

        await commands.RemoveRecoveryCodes(userId);

        Assert.Empty(await new TwoFactorQueries(ctx).GetRecoveryCodes(userId));
    }

    [Fact]
    public async Task HardDeletingTheUser_CascadesToEveryTwoFactorRow()
    {
        var userId = await SeedUser();

        await using (var ctx = _factory.CreateContext())
        {
            var commands = new TwoFactorCommands(ctx);
            await commands.SaveTotp(userId, "SECRET");
            await commands.AddPasskey(userId, Passkey());
            await commands.ReplaceRecoveryCodes(userId, ["a"]);
        }

        await using var cleanup = _factory.CreateContext();
        await cleanup.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");
        await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"Users\" WHERE \"Id\" = {0}", userId);

        Assert.Equal(0, await cleanup.UserTotps.CountAsync());
        Assert.Equal(0, await cleanup.UserPasskeys.CountAsync());
        Assert.Equal(0, await cleanup.UserRecoveryCodes.CountAsync());
    }
}
