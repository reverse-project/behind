using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using Behind.Contact.Domain;

namespace Behind.Contact.WebApi.Endpoints;

public static class ContactValidation
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex FieldKey = new("^[a-zA-Z][a-zA-Z0-9_-]{0,49}$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static string Json<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    public static T Parse<T>(string value) =>
        JsonSerializer.Deserialize<T>(value, JsonOptions) ?? throw new InvalidOperationException("Contact data must not be null.");
    private static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max;
    private static bool Header(string? value, int max) => Text(value, max) && !value!.Any(char.IsControl);
    private static bool Address(string? value) =>
        Header(value, 320) && MailAddress.TryCreate(value, out var address)
        && address.DisplayName.Length == 0 && address.Address.Equals(value, StringComparison.OrdinalIgnoreCase);

    public static bool Valid(SaveContactForm form) =>
        Header(form.Name, 200) && form.Description is { Length: <= 2000 } && (string.IsNullOrEmpty(form.Recipient) || Address(form.Recipient))
        && form.Subjects is { Length: <= 30 } && form.Fields is { Length: <= 30 }
        && (form.SubjectMode == "freeText" && form.Subjects.Length == 0
            || form.SubjectMode == "choices" && form.Subjects.Length > 0 && form.Subjects.All(x => Header(x, 200)) && form.Subjects.Distinct(StringComparer.Ordinal).Count() == form.Subjects.Length)
        && form.Fields.All(x => x is not null && Text(x.Key, 50) && FieldKey.IsMatch(x.Key) && Text(x.Label, 200)
            && x.Type is "text" or "textarea" or "email" && x.MaxLength is >= 1 and <= 2000)
        && form.Fields.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() == form.Fields.Length;

    public static bool Valid(SubmitContactRequest request, ContactForm form, out ContactValue[] values)
    {
        values = [];
        var schema = Schema(form);
        var fields = schema.Fields;
        var subjects = schema.Subjects;
        if (request.Fields is null || !Header(request.Name, 200)
            || !Address(request.Email) || !Header(request.Subject, 200) || !Text(request.Message, 10000)
            || form.SubjectMode == "choices" && !subjects.Contains(request.Subject, StringComparer.Ordinal)
            || request.Fields.Keys.Except(fields.Select(x => x.Key), StringComparer.Ordinal).Any())
        {
            return false;
        }
        var result = new List<ContactValue>();
        foreach (var field in fields)
        {
            request.Fields.TryGetValue(field.Key, out var value);
            value ??= "";
            if (value.Length > field.MaxLength || field.Required && string.IsNullOrWhiteSpace(value)
                || field.Type == "email" && value.Length > 0 && !Address(value))
            {
                return false;
            }
            result.Add(new ContactValue(field.Key, field.Label, value));
        }
        values = result.ToArray();
        return true;
    }

    public static SaveContactForm Schema(ContactForm form)
    {
        var schema = new SaveContactForm(null, form.Name, form.Description, form.Recipient, form.SubjectMode,
            Parse<string[]>(form.SubjectsJson), Parse<ContactField[]>(form.FieldsJson));
        if (!Valid(schema))
        {
            throw new InvalidOperationException("Invalid persisted contact form schema.");
        }
        return schema;
    }

    public static ContactValue[] Values(string json)
    {
        var values = Parse<ContactValue[]>(json);
        if (values.Any(value => value is null || value.Key is null || value.Label is null || value.Value is null))
        {
            throw new InvalidOperationException("Invalid persisted contact request fields.");
        }
        return values;
    }
}
