using Behind.Common;

namespace Behind.Contact.Domain;

public sealed record ContactRequest(VersionedKey<Guid> Id, Guid FormId, string FormName, [property: Sensible] string Recipient, [property: Sensible] string Name, [property: Sensible] string Email, [property: Sensible] string Subject, [property: Sensible] string Message, [property: Sensible] string FieldsJson, string DeliveryStatus, Guid? ReadBy)
{
    public ContactRequest(Guid formId, string formName, string recipient, string name, string email, string subject, string message, string fieldsJson, string deliveryStatus, Guid? readBy)
        : this(new VersionedKey<Guid>(Guid.Empty), formId, formName, recipient, name, email, subject, message, fieldsJson, deliveryStatus, readBy)
    {
    }
}
