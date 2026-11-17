using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Oidc;

public sealed class OidcProviderCatalog
{
    private readonly IOptionsMonitor<AccountsConfiguration>? accounts;

    public OidcConfiguration Configuration { get; }
    public IReadOnlyList<OidcProviderConfiguration> Providers { get; }
    public bool IsEnabled => accounts?.CurrentValue.Oidc ?? true;

    public OidcProviderCatalog(OidcConfiguration configuration, IOptionsMonitor<AccountsConfiguration>? accounts = null)
    {
        this.accounts = accounts;
        Configuration = configuration;
        var failures = new List<string>();
        if (configuration.TicketLifetime <= TimeSpan.Zero
            || configuration.AuthenticationTimeout <= TimeSpan.Zero || configuration.MaxPendingTickets <= 0)
        {
            failures.Add("OIDC ticket lifetime, authentication timeout and pending ticket limit must be positive.");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in configuration.Providers)
        {
            if (string.IsNullOrWhiteSpace(provider.Id)
                || !provider.Id.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
                || !ids.Add(provider.Id))
            {
                failures.Add("OIDC provider IDs must be unique ASCII letters, digits, hyphens or underscores.");
            }

            if (!provider.Enabled)
            {
                continue;
            }

            if (!Uri.TryCreate(provider.Authority, UriKind.Absolute, out var authority)
                || authority.Scheme != "https" || authority.UserInfo.Length != 0
                || authority.Query.Length != 0 || authority.Fragment.Length != 0
                || string.IsNullOrWhiteSpace(provider.ClientId)
                || string.IsNullOrWhiteSpace(provider.DisplayName)
                || string.IsNullOrWhiteSpace(provider.RoleClaimType)
                || !provider.Scopes.Contains("openid", StringComparer.Ordinal)
                || provider.Scopes.Any(string.IsNullOrWhiteSpace)
                || provider.RoleMappings.Any(mapping => string.IsNullOrWhiteSpace(mapping.Key)
                    || mapping.Value is null || mapping.Value.Length == 0
                    || mapping.Value.Any(string.IsNullOrWhiteSpace))
                || (provider.AutoCreateRoles && provider.AutoCreateRolePrefix is not null
                    && string.IsNullOrWhiteSpace(provider.AutoCreateRolePrefix)))
            {
                failures.Add($"OIDC provider '{provider.Id}' has invalid authority, client, scopes or role settings.");
            }
        }

        if (failures.Count != 0)
        {
            throw new OptionsValidationException(OidcConfiguration.SectionName, typeof(OidcConfiguration), failures);
        }

        Providers = configuration.Providers.Where(provider => provider.Enabled).ToArray();
    }

    public OidcProviderConfiguration? Find(string id) =>
        IsEnabled ? Providers.SingleOrDefault(provider => string.Equals(provider.Id, id, StringComparison.Ordinal)) : null;

    public static string Scheme(string id) => $"Behind.Oidc.{id}";
    public static string CallbackPath(string id) => $"/sign/in/oidc/callback/{id}";
}
