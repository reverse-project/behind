using System.Net.Mail;
using System.Text.RegularExpressions;
using Behind.Contact.Domain;

namespace Behind.Contact.WebApi.Endpoints;

public static class ContactValidation
{
    private static readonly Regex FieldKey = new("^[a-zA-Z][a-zA-Z0-9_-]{0,49}$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max;
    private static bool Header(string? value, int max) => Text(value, max) && !value!.Any(char.IsControl);
    private static bool Address(string? value) =>
        Header(value, 320) && MailAddress.TryCreate(value, out var address)
        && address.DisplayName.Length == 0 && address.Address.Equals(value, StringComparison.OrdinalIgnoreCase);

    public static bool Valid(SaveContactForm form) =>
        Header(form.Name, 200) && form.Description is { Length: <= 2000 } && (string.IsNullOrEmpty(form.Recipient) || Address(form.Recipient))
        && form.Subjects is { Length: <= 30 } && form.Fields is { Length: <= 30 }
        && ContactStringEnum.TryParse<ContactSubjectMode>(form.SubjectMode, out var subjectMode)
        && (subjectMode == ContactSubjectMode.FreeText && form.Subjects.Length == 0
            || subjectMode == ContactSubjectMode.Choices && form.Subjects.Length > 0 && form.Subjects.All(x => Header(x, 200)) && form.Subjects.Distinct(StringComparer.Ordinal).Count() == form.Subjects.Length)
        && form.Fields.All(x => x is not null && Text(x.Key, 50) && FieldKey.IsMatch(x.Key) && Text(x.Label, x.Type == ContactFieldType.Boolean ? 2000 : 200)
            && x.Type is ContactFieldType.Text or ContactFieldType.Textarea or ContactFieldType.Email or ContactFieldType.Boolean && x.MaxLength is >= 1 and <= 2000)
        && form.Fields.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() == form.Fields.Length;

    public static bool Valid(SubmitContactRequest request, ContactForm form, out ContactValue[] values)
    {
        values = [];
        var schema = Schema(form);
        var fields = schema.Fields;
        var subjects = schema.Subjects;
        if (request.Fields is null || !Header(request.Name, 200)
            || !Address(request.Email) || !Header(request.Subject, 200) || !Text(request.Message, 10000)
            || form.SubjectMode == ContactSubjectMode.Choices && !subjects.Contains(request.Subject, StringComparer.Ordinal)
            || request.Fields.Keys.Except(fields.Select(x => x.Key), StringComparer.Ordinal).Any())
        {
            return false;
        }
        var result = new List<ContactValue>();
        foreach (var field in fields)
        {
            request.Fields.TryGetValue(field.Key, out var value);
            if (field.Type == ContactFieldType.Boolean)
            {
                value ??= "false";
                if (value is not ("true" or "false") || field.Required && value != "true")
                {
                    return false;
                }
                result.Add(new ContactValue(field.Key, field.Label, value));
                continue;
            }
            value ??= "";
            if (value.Length > field.MaxLength || field.Required && string.IsNullOrWhiteSpace(value)
                || field.Type == ContactFieldType.Email && value.Length > 0 && !Address(value))
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
        if (form.Subjects.Items.IsDefault || form.Fields.Items.IsDefault)
        {
            throw new InvalidOperationException("Invalid persisted contact form schema.");
        }

        var schema = new SaveContactForm(null, form.Name, form.Description, form.Recipient, ContactStringEnum.ToToken(form.SubjectMode),
            form.Subjects.Items.ToArray(), form.Fields.Items.ToArray());
        if (!Valid(schema))
        {
            throw new InvalidOperationException("Invalid persisted contact form schema.");
        }
        return schema;
    }

    public static ContactValue[] Values(ContactFieldValues fields)
    {
        var values = fields.Items;
        if (values.IsDefault || values.Any(value => value is null || value.Key is null || value.Label is null || value.Value is null))
        {
            throw new InvalidOperationException("Invalid persisted contact request fields.");
        }
        return values.ToArray();
    }
}
