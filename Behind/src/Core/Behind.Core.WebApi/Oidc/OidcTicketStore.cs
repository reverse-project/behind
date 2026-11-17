using System.Security.Cryptography;
using System.Text;

namespace Behind.Core.WebApi.Oidc;

public sealed record OidcTicket(OidcIdentity Identity, string FlowId, byte[] BrowserProof, DateTimeOffset Expires);

public sealed class OidcTicketStore(OidcProviderCatalog catalog, TimeProvider timeProvider)
{
    public bool IsEnabled => catalog.IsEnabled;
    private readonly object gate = new();
    private readonly Dictionary<string, OidcTicket> tickets = new(StringComparer.Ordinal);

    public string Issue(OidcIdentity identity, string flowId, string browserProof)
    {
        lock (gate)
        {
            if (!IsEnabled)
            {
                throw new InvalidOperationException("OIDC sign-in is globally disabled.");
            }

            var now = timeProvider.GetUtcNow();
            foreach (var expired in tickets.Where(pair => pair.Value.Expires <= now).Select(pair => pair.Key).ToArray())
            {
                tickets.Remove(expired);
            }

            if (tickets.Count >= catalog.Configuration.MaxPendingTickets)
            {
                throw new InvalidOperationException("The OIDC pending ticket limit was reached.");
            }

            var code = RandomNumberGenerator.GetHexString(32);
            tickets.Add(code, new OidcTicket(identity, flowId, Hash(browserProof),
                now + catalog.Configuration.TicketLifetime));
            return code;
        }
    }

    public OidcTicket? Redeem(string code, Func<string, string?> browserProof)
    {
        lock (gate)
        {
            if (!tickets.TryGetValue(code, out var ticket))
            {
                return null;
            }

            if (ticket.Expires <= timeProvider.GetUtcNow())
            {
                tickets.Remove(code);
                return null;
            }

            var proof = browserProof(ticket.FlowId);
            if (proof is null || !CryptographicOperations.FixedTimeEquals(ticket.BrowserProof, Hash(proof)))
            {
                return null;
            }

            tickets.Remove(code);
            return ticket;
        }
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
