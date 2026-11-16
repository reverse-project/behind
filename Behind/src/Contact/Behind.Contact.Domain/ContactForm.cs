using Behind.Common;

namespace Behind.Contact.Domain;

public sealed record ContactForm(
    VersionedKey<Guid> Id,
    string Name,
    string Description,
    [property: Sensible] string Recipient,
    ContactSubjectMode SubjectMode,
    ContactSubjects Subjects,
    ContactFieldSchema Fields)
{
    public ContactForm(
        string name,
        string description,
        string recipient,
        ContactSubjectMode subjectMode,
        ContactSubjects subjects,
        ContactFieldSchema fields)
        : this(new VersionedKey<Guid>(Guid.Empty), name, description, recipient, subjectMode, subjects, fields)
    {
    }

    public ContactForm(
        VersionedKey<Guid> id,
        string name,
        string description,
        string recipient,
        string subjectMode,
        ContactSubjects subjects,
        ContactFieldSchema fields)
        : this(id, name, description, recipient, ContactStringEnum.Parse<ContactSubjectMode>(subjectMode), subjects, fields)
    {
    }

    public ContactForm(
        string name,
        string description,
        string recipient,
        string subjectMode,
        ContactSubjects subjects,
        ContactFieldSchema fields)
        : this(new VersionedKey<Guid>(Guid.Empty), name, description, recipient, subjectMode, subjects, fields)
    {
    }
}
