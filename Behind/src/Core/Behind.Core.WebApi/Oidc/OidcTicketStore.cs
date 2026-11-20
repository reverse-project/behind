using System.Security.Cryptography;
using System.Text;

namespace Behind.Core.WebApi.Oidc;

public sealed record OidcTicket(OidcIdentity Identity, string FlowId, byte[] BrowserProof, DateTimeOffset Expires);

public sealed class OidcTicketStore(OidcProviderCatalog catalog, TimeProvider timeProvider)
{
    public bool IsEnabled => catalog.IsEnabled;
    private readonly object gate = new();
    private readonly Dictionary<string, OidcTicket> tickets = new(StringComparer.Ordinal);

    private readonly HashSet<string> reserved = new(StringComparer.Ordinal);

    public string Issue(OidcIdentity identity, string flowId, string browserProof)
    {
        lock (gate)
        {
            if (!IsEnabled)
            {
                throw new InvalidOperationException("OIDC sign-in is globally disabled.");
            }

            var now = timeProvider.GetUtcNow();
            foreach (var expired in tickets.Where(pair => pair.Value.Expires <= now && !reserved.Contains(pair.Key)).Select(pair => pair.Key).ToArray())
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
        using var reservation = Reserve(code, browserProof);
        reservation?.Commit();
        return reservation?.Ticket;
    }

    public Reservation? Reserve(string code, Func<string, string?> browserProof)
    {
        lock (gate)
        {
            if (reserved.Contains(code) || !tickets.TryGetValue(code, out var ticket))
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

            reserved.Add(code);
            return new Reservation(this, code, ticket);
        }
    }

    public sealed class Reservation : IDisposable
    {
        private readonly OidcTicketStore store;
        private readonly string code;
        private bool completed;

        internal Reservation(OidcTicketStore store, string code, OidcTicket ticket)
        {
            this.store = store;
            this.code = code;
            Ticket = ticket;
        }

        public OidcTicket Ticket { get; }

        public void Commit() => Complete(true);

        public void Dispose() => Complete(false);

        private void Complete(bool consume)
        {
            lock (store.gate)
            {
                if (completed)
                {
                    return;
                }
                completed = true;
                store.reserved.Remove(code);
                if (consume)
                {
                    store.tickets.Remove(code);
                }
            }
        }
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
