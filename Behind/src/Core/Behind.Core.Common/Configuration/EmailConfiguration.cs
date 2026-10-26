using System.ComponentModel;
using System.Text.Json.Serialization;
using Behind.Common;
using Behind.Core.Common.Converters;

namespace Behind.Core.Common.Configuration;

[TypeConverter(typeof(EmailConfigurationTypeConverter))]
[JsonConverter(typeof(EmailConfigurationJsonConverter))]
public abstract record EmailConfiguration(string Provider)
{
    public const string SectionName = "Email";
}

public sealed record SmtpEmailConfiguration(string SmtpServer, int Port, Ssl Ssl, Option<Authentication> Authentication) : EmailConfiguration("Smtp");

public sealed record LoggerEmailConfiguration() : EmailConfiguration("Logger");

[TypeConverter(typeof(EmailAuthenticationTypeConverter))]
[JsonConverter(typeof(EmailAuthenticationJsonConverter))]
public abstract record Authentication(string Type);

public sealed record NoAuthentication() : Authentication("No");

public sealed record BasicAuthentication(string Username, [property: Sensible] string Password) : Authentication("Basic");

public sealed record Ssl(bool Enabled);