using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Common.Configuration;
using Behind.Core.Common.Configuration;

namespace Behind.Core.WebApi.Settings;

public static class OidcSettingsExtensions
{
    private static readonly JsonSerializerOptions WriteOptions = new();

    public static bool IsOidcPath(string file, string path) =>
        string.Equals(file, "security.json", StringComparison.OrdinalIgnoreCase)
        && (string.Equals(path, OidcConfiguration.SectionName, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith($"{OidcConfiguration.SectionName}:", StringComparison.OrdinalIgnoreCase));

    extension(OidcConfiguration configuration)
    {
        public OidcSettings ToOidcSettings() => new()
        {
            TicketLifetime = configuration.TicketLifetime,
            AuthenticationTimeout = configuration.AuthenticationTimeout,
            MaxPendingTickets = configuration.MaxPendingTickets,
            Providers = configuration.Providers.Select(provider => new OidcProviderSettings
            {
                Id = provider.Id,
                DisplayName = provider.DisplayName,
                Enabled = provider.Enabled,
                Authority = provider.Authority,
                ClientId = provider.ClientId,
                HasClientSecret = !string.IsNullOrEmpty(provider.ClientSecret),
                Scopes = provider.Scopes,
                AutoCreateUsers = provider.AutoCreateUsers,
                LinkByVerifiedEmail = provider.LinkByVerifiedEmail,
                RoleClaimType = provider.RoleClaimType,
                RoleMappings = provider.RoleMappings,
                AutoCreateRoles = provider.AutoCreateRoles,
                AutoCreateRolePrefix = provider.AutoCreateRolePrefix
            }).ToArray()
        };
    }

    extension(OidcSettings request)
    {
        public OidcConfiguration ApplyOidcSettings(OidcConfiguration stored)
        {
            if (request.Providers is null || request.Providers.Any(provider => provider is null
                    || provider.Scopes is null || provider.RoleMappings is null || provider.ClientSecret is null
                    || (provider.ClearClientSecret && provider.ClientSecret.Length != 0)))
            {
                throw new ArgumentException("OIDC providers, scopes, role mappings and client secrets must be valid.");
            }

            var previous = stored.Providers.ToDictionary(provider => provider.Id, StringComparer.Ordinal);
            return new OidcConfiguration
            {
                TicketLifetime = request.TicketLifetime,
                AuthenticationTimeout = request.AuthenticationTimeout,
                MaxPendingTickets = request.MaxPendingTickets,
                Providers = request.Providers.Select(provider => new OidcProviderConfiguration
                {
                    Id = provider.Id,
                    DisplayName = provider.DisplayName,
                    Enabled = provider.Enabled,
                    Authority = provider.Authority,
                    ClientId = provider.ClientId,
                    ClientSecret = provider.ClearClientSecret ? string.Empty
                        : provider.ClientSecret.Length != 0 ? provider.ClientSecret
                        : previous.TryGetValue(provider.Id, out var existing) ? existing.ClientSecret : string.Empty,
                    Scopes = provider.Scopes,
                    AutoCreateUsers = provider.AutoCreateUsers,
                    LinkByVerifiedEmail = provider.LinkByVerifiedEmail,
                    RoleClaimType = provider.RoleClaimType,
                    RoleMappings = provider.RoleMappings,
                    AutoCreateRoles = provider.AutoCreateRoles,
                    AutoCreateRolePrefix = provider.AutoCreateRolePrefix
                }).ToArray()
            };
        }
    }

    extension(JsonObject root)
    {
        public string OidcSettingsPath()
        {
            var securityName = root.Select(property => property.Key)
                .FirstOrDefault(name => string.Equals(name, "Security", StringComparison.OrdinalIgnoreCase)) ?? "Security";
            var security = root[securityName] as JsonObject;
            var oidcName = security?.Select(property => property.Key)
                .FirstOrDefault(name => string.Equals(name, "Oidc", StringComparison.OrdinalIgnoreCase)) ?? "Oidc";
            return $"{securityName}:{oidcName}";
        }

        public JsonObject MergeOidcSettings(OidcConfiguration configuration)
        {
            var original = root.FindProperty("Security") as JsonObject;
            var oidc = original?.FindProperty("Oidc") as JsonObject;
            var result = oidc?.DeepClone().AsObject() ?? new JsonObject();
            var providers = oidc?.FindProperty("Providers") as JsonArray;
            var previous = (providers ?? []).OfType<JsonObject>().ToDictionary(
                provider => provider.FindProperty("Id")?.GetValue<string>() ?? string.Empty,
                StringComparer.Ordinal);
            var serialized = JsonSerializer.SerializeToNode(configuration, WriteOptions)!.AsObject();
            var mergedProviders = new JsonArray();
            foreach (var provider in configuration.Providers)
            {
                var merged = previous.TryGetValue(provider.Id, out var old)
                    ? old.DeepClone().AsObject() : new JsonObject();
                foreach (var property in JsonSerializer.SerializeToNode(provider, WriteOptions)!.AsObject())
                {
                    merged.ReplaceProperty(property.Key, property.Value?.DeepClone());
                }

                mergedProviders.Add(merged);
            }

            foreach (var property in serialized)
            {
                result.ReplaceProperty(property.Key,
                    property.Key == nameof(OidcConfiguration.Providers) ? mergedProviders : property.Value?.DeepClone());
            }

            return result;
        }
    }

    extension(JsonObject container)
    {
        private JsonNode? FindProperty(string name) =>
            container.FirstOrDefault(property => string.Equals(property.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

        private void ReplaceProperty(string name, JsonNode? value)
        {
            var existing = container.Select(property => property.Key)
                .FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));
            container[existing ?? name] = value;
        }
    }
}
