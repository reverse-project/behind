using System.Security.Claims;
using System.Text.Json;
using Behind.Core.Common.Configuration;

namespace Behind.Core.WebApi.Oidc;

public sealed record OidcIdentity(
    string ProviderId, string Issuer, string Subject, string? Email, bool EmailVerified,
    string? DisplayName, IReadOnlyList<string> Roles)
{
    public static OidcIdentity FromPrincipal(OidcProviderConfiguration provider, ClaimsPrincipal principal)
    {
        var issuer = principal.FindFirst("iss")?.Value;
        var subject = principal.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
        {
            throw new InvalidOperationException("The OIDC identity has no issuer or subject.");
        }

        var roles = new HashSet<string>(StringComparer.Ordinal);
        var path = provider.RoleClaimType.Split('.');
        var direct = principal.FindAll(provider.RoleClaimType).ToArray();
        foreach (var claim in direct.Length != 0 ? direct : principal.FindAll(path[0]))
        {
            if (direct.Length == 0 && path.Length > 1)
            {
                using var document = JsonDocument.Parse(claim.Value);
                var value = document.RootElement;
                foreach (var segment in path.Skip(1))
                {
                    if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
                    {
                        throw new InvalidOperationException("The configured OIDC role claim path is invalid.");
                    }
                }

                ReadRoles(value, roles);
            }
            else if (claim.Value.TrimStart().StartsWith('['))
            {
                using var document = JsonDocument.Parse(claim.Value);
                ReadRoles(document.RootElement, roles);
            }
            else if (!string.IsNullOrWhiteSpace(claim.Value))
            {
                roles.Add(claim.Value);
            }
        }

        return new OidcIdentity(provider.Id, issuer, subject, principal.FindFirst("email")?.Value,
            string.Equals(principal.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase),
            principal.FindFirst("name")?.Value, roles.ToArray());
    }

    private static void ReadRoles(JsonElement value, HashSet<string> roles)
    {
        if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            roles.Add(value.GetString()!);
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
                {
                    throw new InvalidOperationException("OIDC roles must be nonempty strings.");
                }

                roles.Add(item.GetString()!);
            }
        }
        else
        {
            throw new InvalidOperationException("OIDC roles must be a string or an array of strings.");
        }
    }
}
