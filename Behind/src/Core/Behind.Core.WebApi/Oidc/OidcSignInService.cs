using System.ComponentModel.DataAnnotations;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Oidc;

public interface IOidcSignInService
{
    Task<ApplicationUser?> Resolve(OidcIdentity identity, CancellationToken cancellationToken);
}

public sealed class OidcSignInService(
    OidcProviderCatalog catalog,
    IOidcAccountStore accounts,
    UserManager<ApplicationUser> users,
    IOptions<AccountsConfiguration> accountConfiguration,
    IAccessSettingsQueries accessSettings,
    TimeProvider timeProvider,
    ILogger<OidcSignInService> logger) : IOidcSignInService
{
    public async Task<ApplicationUser?> Resolve(OidcIdentity identity, CancellationToken cancellationToken)
    {
        var provider = catalog.Find(identity.ProviderId);
        if (provider is null || !accountConfiguration.Value.SignIn)
        {
            logger.LogWarning("OIDC account resolution rejected: provider or sign-in is disabled");
            return null;
        }

        var userId = await accounts.FindUser(identity.ProviderId, identity.Issuer, identity.Subject, cancellationToken);
        ApplicationUser? user;
        if (userId is not null)
        {
            user = await users.FindByIdAsync(userId.Value.ToString());
            if (user is null)
            {
                logger.LogWarning("OIDC account resolution rejected: linked user {UserId} is unavailable", userId);
                return null;
            }
        }
        else
        {
            var email = identity.Email?.Trim().ToLowerInvariant();
            if (!identity.EmailVerified || string.IsNullOrWhiteSpace(email)
                || !new EmailAddressAttribute().IsValid(email))
            {
                logger.LogWarning("OIDC account resolution rejected: a verified email is required for first sign-in");
                return null;
            }

            user = await users.FindByEmailAsync(email);
            if (user is not null)
            {
                if (!provider.LinkByVerifiedEmail || !user.Emails.Any(address =>
                        address.ConfirmedAt is not null && string.Equals(address.Value, email, StringComparison.OrdinalIgnoreCase)))
                {
                    logger.LogWarning("OIDC account resolution rejected: verified-email linking is not permitted");
                    return null;
                }

                await accounts.Link(user.Id, identity.ProviderId, identity.Issuer, identity.Subject, cancellationToken);
            }
            else
            {
                if (!provider.AutoCreateUsers || !accountConfiguration.Value.SignUp)
                {
                    logger.LogWarning("OIDC account resolution rejected: account creation is disabled");
                    return null;
                }

                var newUser = new ApplicationUser
                {
                    DisplayName = identity.DisplayName.ToDisplayName(),
                    Emails = [new Email(email, timeProvider.GetUtcNow())],
                    PendingApproval = (await accessSettings.Current(cancellationToken)).ModeratedSignUp
                };
                var linked = await accounts.CreateAndLink(
                    async () =>
                    {
                        var created = await users.CreateAsync(newUser);
                        if (!created.Succeeded)
                        {
                            logger.LogWarning("OIDC account creation failed ({ErrorCodes})",
                                string.Join(", ", created.Errors.Select(error => error.Code)));
                        }

                        return created.Succeeded;
                    },
                    newUser.Id, identity.ProviderId, identity.Issuer, identity.Subject, cancellationToken);
                if (!linked)
                {
                    return null;
                }

                user = newUser;
            }
        }

        var mappedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var autoRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var externalRole in identity.Roles)
        {
            if (TryGetRoleMapping(provider.RoleMappings, externalRole, out var mapped))
            {
                mappedRoles.UnionWith(mapped);
            }
            else if (provider.AutoCreateRoles)
            {
                autoRoles.Add((provider.AutoCreateRolePrefix ?? $"oidc:{provider.Id}:") + externalRole);
            }
        }

        await accounts.SynchronizeRoles(user.Id, identity.ProviderId, mappedRoles.ToArray(), autoRoles.ToArray(), cancellationToken);
        return user;
    }

    private static bool TryGetRoleMapping(
        IReadOnlyDictionary<string, string[]> roleMappings,
        string externalRole,
        out string[] mappedRoles)
    {
        foreach (var mapping in roleMappings)
        {
            if (string.Equals(mapping.Key, externalRole, StringComparison.Ordinal))
            {
                mappedRoles = mapping.Value;
                return true;
            }
        }

        mappedRoles = [];
        return false;
    }
}
