using System.ComponentModel;
using System.Net.Mail;
using System.Text.Json.Serialization;
using Behind.Core.Common.Converters;

namespace Behind.Core.Common.Email;

[TypeConverter(typeof(EmailAddressTypeConverter))]
[JsonConverter(typeof(EmailAddressJsonConverter))]
public sealed record EmailAddress
{
    public required string Address
    {
        get;
        init =>
            field = MailAddress.TryCreate(value, out var mailAddress)
                ? mailAddress.Address
                : throw new ArgumentException("Invalid email address", nameof(value));
    }

    public override string ToString() => Address;
}