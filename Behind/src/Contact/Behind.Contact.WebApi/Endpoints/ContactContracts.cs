namespace Behind.Contact.WebApi.Endpoints;

public sealed record ContactField(string Key, string Label, string Type, bool Required, int MaxLength);
public sealed record ContactValue(string Key, string Label, string Value);
public sealed record SaveContactForm(long? Version, string Name, string Description, string? Recipient, string SubjectMode, string[] Subjects, ContactField[] Fields);
public sealed record SubmitContactRequest(string Name, string Email, string Subject, string Message, Dictionary<string, string> Fields);
public sealed record ContactFormResponse(Guid Id, long Version, string Name, string Description, string SubjectMode, string[] Subjects, ContactField[] Fields);
public sealed record ManagedContactForm(Guid Id, long Version, string Name, string Description, string Recipient, string SubjectMode, string[] Subjects, ContactField[] Fields, bool IsDeleted);
public sealed record ContactRequestResponse(Guid Id, long Version, Guid FormId, string FormName, string Recipient, string Name, string Email, string Subject, string Message, ContactValue[] Fields, string DeliveryStatus, Guid? ReadBy, bool IsDeleted, DateTimeOffset? CreatedAt, string? ReadByName);
public sealed record SetReadRequest(bool Read);

public sealed record ContactReceipt(string DeliveryStatus);
