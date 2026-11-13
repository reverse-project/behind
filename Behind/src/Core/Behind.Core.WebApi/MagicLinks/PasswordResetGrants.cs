using Behind.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.Extensions.Options;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.MagicLinks;

public sealed record PasswordResetGrant(string Token, DateTimeOffset ExpiresAt);

public interface IPasswordResetGrants
{
    Task<Option<PasswordResetGrant>> Issue(Guid userId, CancellationToken cancellationToken = default);

    Option<TokenNonce> Verify(string? token);
}

public sealed class PasswordResetGrants(
    IMagicLinkSettingsStore settingsStore,
    IOptions<SecurityConfiguration> security,
    IMagicLinkCommands magicLinkCommands,
    TimeProvider timeProvider) : IPasswordResetGrants
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public async Task<Option<PasswordResetGrant>> Issue(Guid userId, CancellationToken cancellationToken = default)
    {
        var signing =
            from settings in settingsStore.Current
            from jwt in security.Value.Jwt
            select (Secret: settings.ActualSecret, Pepper: jwt.SigningKey);

        return await signing.Match(
            none: () => Task.FromResult<Option<PasswordResetGrant>>(None),
            some: async keys =>
            {
                var grant = MagicLink.Create(timeProvider.GetUtcNow(), Lifetime, MagicLinkFunction.PasswordReset);
                await magicLinkCommands.Issue(userId, grant, cancellationToken);

                var credential = MagicLinkCredential.Create(
                    grant.Nonce, keys.Secret, keys.Pepper, MagicLinkCredential.PasswordResetPurpose);

                return Some(new PasswordResetGrant(credential.Token, grant.ExpiresAt));
            });
    }

    public Option<TokenNonce> Verify(string? token)
    {
        var now = timeProvider.GetUtcNow();

        return
            from settings in settingsStore.Current
            from jwt in security.Value.Jwt
            from credential in MagicLinkCredential.TryParse(token)
            where credential.IsSignedBy(settings.ValidSecrets(now), jwt.SigningKey, MagicLinkCredential.PasswordResetPurpose)
            select credential.Nonce;
    }
}
