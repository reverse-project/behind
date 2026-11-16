using Behind.Common;

namespace Behind.Contact.Domain;

public sealed record ContactForm(VersionedKey<Guid> Id, string Name, string Description, [property: Sensible] string Recipient, string SubjectMode, string SubjectsJson, string FieldsJson)
{
    public ContactForm(string name, string description, string recipient, string subjectMode, string subjectsJson, string fieldsJson)
        : this(new VersionedKey<Guid>(Guid.Empty), name, description, recipient, subjectMode, subjectsJson, fieldsJson)
    {
    }
}
