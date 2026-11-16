using Behind.Common;

namespace Behind.Contact.Domain;

public sealed record ContactRequest(
    VersionedKey<Guid> Id,
    Guid FormId,
    string FormName,
    [property: Sensible] string Recipient,
    [property: Sensible] string Name,
    [property: Sensible] string Email,
    [property: Sensible] string Subject,
    [property: Sensible] string Message,
    [property: Sensible] ContactFieldValues Fields,
    ContactDeliveryStatus DeliveryStatus,
    Guid? ReadBy)
{
    public ContactRequest(
        Guid formId,
        string formName,
        string recipient,
        string name,
        string email,
        string subject,
        string message,
        ContactFieldValues fields,
        ContactDeliveryStatus deliveryStatus,
        Guid? readBy)
        : this(new VersionedKey<Guid>(Guid.Empty), formId, formName, recipient, name, email, subject, message, fields, deliveryStatus, readBy)
    {
    }

    public ContactRequest(
        VersionedKey<Guid> id,
        Guid formId,
        string formName,
        string recipient,
        string name,
        string email,
        string subject,
        string message,
        ContactFieldValues fields,
        string deliveryStatus,
        Guid? readBy)
        : this(id, formId, formName, recipient, name, email, subject, message, fields,
            ContactStringEnum.Parse<ContactDeliveryStatus>(deliveryStatus), readBy)
    {
    }

    public ContactRequest(
        Guid formId,
        string formName,
        string recipient,
        string name,
        string email,
        string subject,
        string message,
        ContactFieldValues fields,
        string deliveryStatus,
        Guid? readBy)
        : this(new VersionedKey<Guid>(Guid.Empty), formId, formName, recipient, name, email, subject, message, fields, deliveryStatus, readBy)
    {
    }
}
