using System.ComponentModel;
using System.Text.Json.Serialization;
using Behind.Common;
using Behind.Common.Templating;
using Behind.Core.Common.Converters;

namespace Behind.Core.Common.Configuration;

[TypeConverter(typeof(EmailConfigurationTypeConverter))]
[JsonConverter(typeof(EmailConfigurationJsonConverter))]
public abstract record EmailConfiguration(string Provider)
{
    public const string SectionName = "Email";

    public static readonly IReadOnlyList<string> Providers = [LoggerEmailConfiguration.ProviderName, SmtpEmailConfiguration.ProviderName];
}

public sealed record SmtpEmailConfiguration(string SmtpServer, int Port, Ssl Ssl, Option<Authentication> Authentication, Option<Templates> Templates, Option<string> From = default) : EmailConfiguration(ProviderName)
{
    public const string ProviderName = "Smtp";
}

public sealed record LoggerEmailConfiguration() : EmailConfiguration(ProviderName)
{
    public const string ProviderName = "Logger";
}

[TypeConverter(typeof(EmailAuthenticationTypeConverter))]
[JsonConverter(typeof(EmailAuthenticationJsonConverter))]
public abstract record Authentication(string Type)
{
    public static readonly IReadOnlyList<string> Types = [NoAuthentication.TypeName, BasicAuthentication.TypeName];
}

public sealed record NoAuthentication() : Authentication(TypeName)
{
    public const string TypeName = "No";
}

public sealed record BasicAuthentication(string Username, [property: Sensible] string Password) : Authentication(TypeName)
{
    public const string TypeName = "Basic";
}

public sealed record Ssl(bool Enabled);