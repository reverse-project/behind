using Behind.Common;

namespace Behind.Core.Domain;

public sealed record PrivateMessageEmailDelivery(
    VersionedKey<Guid> Id,
    [property: Sensible] Guid ConversationId,
    [property: Sensible] Guid MessageId,
    [property: Sensible] Guid UserId,
    [property: Sensible] string Email,
    string Status,
    int Attempts)
{
    private PrivateMessageEmailDelivery() : this(new(Guid.Empty), Guid.Empty, Guid.Empty, Guid.Empty, "", "pending", 0)
    {
    }
}
