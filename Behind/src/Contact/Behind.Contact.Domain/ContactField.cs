namespace Behind.Contact.Domain;

public sealed record ContactField(string Key, string Label, ContactFieldType Type, bool Required, int MaxLength);

public sealed record ContactValue(string Key, string Label, string Value);
