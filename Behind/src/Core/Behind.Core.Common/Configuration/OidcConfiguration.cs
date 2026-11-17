using Behind.Common;

namespace Behind.Core.Common.Configuration;

public sealed record OidcConfiguration
{
    public const string SectionName = $"{SecurityConfiguration.SectionName}:Oidc";

    public OidcProviderConfiguration[] Providers { get; init; } = [];
    public TimeSpan TicketLifetime { get; init; } = TimeSpan.FromMinutes(2);
    public int MaxPendingTickets { get; init; } = 1000;
    public TimeSpan AuthenticationTimeout { get; init; } = TimeSpan.FromMinutes(10);
}

public sealed record OidcProviderConfiguration
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
    public string Authority { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    [Sensible]
    public string ClientSecret { get; init; } = string.Empty;
    public string[] Scopes { get; init; } = ["openid", "profile", "email"];
    public bool AutoCreateUsers { get; init; } = true;
    public bool LinkByVerifiedEmail { get; init; }
    public string RoleClaimType { get; init; } = "roles";
    public Dictionary<string, string[]> RoleMappings { get; init; } = new(StringComparer.Ordinal);
    public bool AutoCreateRoles { get; init; }
    public string? AutoCreateRolePrefix { get; init; }
}
