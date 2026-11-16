using Behind.Common;
using Behind.Core.Common.Configuration;

namespace Behind.Core.WebApi.Settings;

public sealed record OidcSettings
{
    public TimeSpan TicketLifetime { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan AuthenticationTimeout { get; init; } = TimeSpan.FromMinutes(10);
    public int MaxPendingTickets { get; init; } = 1000;
    public OidcProviderSettings[] Providers { get; init; } = [];
}

public sealed record OidcProviderSettings
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
    public string Authority { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    [Sensible]
    public string ClientSecret { get; init; } = string.Empty;
    public bool HasClientSecret { get; init; }
    public bool ClearClientSecret { get; init; }
    public string[] Scopes { get; init; } = ["openid", "profile", "email"];
    public bool AutoCreateUsers { get; init; } = true;
    public bool LinkByVerifiedEmail { get; init; }
    public string RoleClaimType { get; init; } = "roles";
    public Dictionary<string, string[]> RoleMappings { get; init; } = new(StringComparer.Ordinal);
    public bool AutoCreateRoles { get; init; }
    public string? AutoCreateRolePrefix { get; init; }
}
