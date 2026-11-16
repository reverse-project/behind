using Behind.Core.Common.Configuration;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class AccountModeFilterTests
{
    private static async Task<object?> Invoke(
        AccountMode mode, AccountsConfiguration settings, object? request = null, ITwoFactorTickets? tickets = null)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IOptionsSnapshot<AccountsConfiguration>>(FixedOptionsSnapshot.Of(settings))
            .AddSingleton(tickets ?? Substitute.For<ITwoFactorTickets>())
            .BuildServiceProvider();
        await using (services)
        {
            var context = new DefaultEndpointFilterInvocationContext(
                new DefaultHttpContext { RequestServices = services },
                request is null ? [] : [request]);
            return await new AccountModeFilter(mode).InvokeAsync(context, _ => ValueTask.FromResult<object?>("next"));
        }
    }

    [Theory]
    [InlineData(AccountMode.Password)]
    [InlineData(AccountMode.MagicLink)]
    [InlineData(AccountMode.TwoFactor)]
    [InlineData(AccountMode.Passkey)]
    public async Task DisabledModesAreRejected(AccountMode mode)
    {
        var settings = new AccountsConfiguration { Password = false, MagicLink = false, TwoFactor = false, Passkey = false };
        Assert.IsType<NotFound>(await Invoke(mode, settings));
        Assert.Equal("next", await Invoke(mode, new AccountsConfiguration()));
    }

    [Fact]
    public async Task DisabledMagicLinkStillAllowsRecoveryRequests()
    {
        var settings = new AccountsConfiguration { MagicLink = false };

        Assert.IsType<NotFound>(await Invoke(AccountMode.MagicLink, settings,
            new SignInEndpoints.MagicLinkRequest("user@example.test")));
        Assert.Equal("next", await Invoke(AccountMode.MagicLink, settings,
            new SignInEndpoints.MagicLinkRequest("user@example.test", MagicLinkFunction.LostPassword)));
    }

    [Fact]
    public async Task DisabledTwoFactorAlsoRejectsPasskeys()
    {
        Assert.IsType<NotFound>(await Invoke(AccountMode.Passkey, new AccountsConfiguration { TwoFactor = false }));
    }

    [Fact]
    public async Task PrimaryPasskeysRemainEnabledWhenTwoFactorIsDisabled()
    {
        Assert.Equal("next", await Invoke(AccountMode.PrimaryPasskey,
            new AccountsConfiguration { TwoFactor = false, Passkey = true }));
        Assert.IsType<NotFound>(await Invoke(AccountMode.PrimaryPasskey,
            new AccountsConfiguration { Passkey = false }));
    }

    [Fact]
    public async Task ExistingPasskeyChallengeIsRejectedWhenPasskeysAreDisabled()
    {
        var tickets = Substitute.For<ITwoFactorTickets>();
        tickets.Open("ticket").Returns(Some(new TwoFactorTicket(
            Guid.NewGuid(), TwoFactorStage.Verify, DateTimeOffset.UtcNow.AddMinutes(1), PrimaryCredentialId: "primary")));

        Assert.IsType<NotFound>(await Invoke(AccountMode.TwoFactor, new AccountsConfiguration { Passkey = false },
            new TwoFactorSignInEndpoints.CodeRequest("ticket", "123456"), tickets));
    }

    [Theory]
    [InlineData(MagicLinkFunction.SignIn, false)]
    [InlineData(MagicLinkFunction.LostPassword, true)]
    public async Task ExistingTwoFactorTicketsRespectMagicLinkSwitch(MagicLinkFunction function, bool allowed)
    {
        var tickets = Substitute.For<ITwoFactorTickets>();
        tickets.Open("ticket").Returns(Some(new TwoFactorTicket(
            Guid.NewGuid(), TwoFactorStage.Verify, DateTimeOffset.UtcNow.AddMinutes(1), function)));

        var result = await Invoke(AccountMode.TwoFactor, new AccountsConfiguration { MagicLink = false },
            new TwoFactorSignInEndpoints.CodeRequest("ticket", "123456"), tickets);

        if (allowed)
        {
            Assert.Equal("next", result);
        }
        else
        {
            Assert.IsType<NotFound>(result);
        }
    }

    [Fact]
    public async Task ExistingPasswordChallengeIsRejectedWhenPasswordIsDisabled()
    {
        var tickets = Substitute.For<ITwoFactorTickets>();
        tickets.Open("ticket").Returns(Some(new TwoFactorTicket(
            Guid.NewGuid(), TwoFactorStage.Verify, DateTimeOffset.UtcNow.AddMinutes(1))));

        Assert.IsType<NotFound>(await Invoke(AccountMode.TwoFactor,
            new AccountsConfiguration { Password = false },
            new TwoFactorSignInEndpoints.CodeRequest("ticket", "123456"), tickets));
    }
}
