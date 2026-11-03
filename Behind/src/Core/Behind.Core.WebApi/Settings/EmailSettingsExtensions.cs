using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Common;
using Behind.Common.Configuration;
using Behind.Common.Templating;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.EmailSender;
using Microsoft.Extensions.Configuration;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Settings;

public static class EmailSettingsExtensions
{
    private const int MaxPort = 65535;

    private static readonly JsonSerializerOptions WriteOptions = new();

    private static readonly IReadOnlyDictionary<string, string> NoTemplates = new Dictionary<string, string>();

    private static readonly IReadOnlyList<string> TemplateNames = [..DefaultEmailTemplates.All.Select(template => template.Name)];

    extension(FileInfo file)
    {
        public Option<EmailConfiguration> ReadEmailConfiguration()
        {
            var bound = new ConfigurationBuilder()
                .AddJsonFile(file.FullName, optional: false, reloadOnChange: false)
                .Build()
                .GetSection(EmailConfiguration.SectionName)
                .GetOptionAware<EmailConfiguration>();

            return bound is null ? None : Some(bound);
        }

        public Task WriteEmailConfigurationAsync(EmailConfiguration configuration, CancellationToken cancellationToken = default) =>
            file.SetJsonNodesAsync(
                new Dictionary<string, JsonNode?>
                {
                    [EmailConfiguration.SectionName] = JsonSerializer
                        .SerializeToNode(configuration, typeof(EmailConfiguration), WriteOptions)!
                        .WithoutNulls()
                },
                cancellationToken);
    }

    extension(Option<EmailConfiguration> current)
    {
        public EmailSettingsResponse ToSettings() =>
            new(
                current.Match(() => LoggerEmailConfiguration.ProviderName, configuration => configuration.Provider),
                EmailConfiguration.Providers,
                Authentication.Types,
                DefaultEmailTemplates.All.ToDictionary(template => template.Name, template => template.Content),
                current.Match(() => SmtpSettingsResponse.Default, configuration => configuration.ToSmtpSettings()));

        public Option<EmailConfiguration> Apply(EmailSettingsRequest request) =>
            request.Provider switch
            {
                LoggerEmailConfiguration.ProviderName => Some<EmailConfiguration>(new LoggerEmailConfiguration()),
                SmtpEmailConfiguration.ProviderName when request.Smtp is { } smtp =>
                    smtp.ToConfiguration(current.StoredPassword),
                _ => None
            };

        private string StoredPassword =>
            current.Match(
                () => string.Empty,
                configuration => configuration is SmtpEmailConfiguration smtp
                    ? smtp.Authentication.Match(() => string.Empty, authentication => authentication.Password)
                    : string.Empty);
    }

    extension(Authentication authentication)
    {
        private string Password => authentication is BasicAuthentication basic ? basic.Password : string.Empty;

        private string Username => authentication is BasicAuthentication basic ? basic.Username : string.Empty;
    }

    extension(EmailConfiguration configuration)
    {
        private SmtpSettingsResponse ToSmtpSettings() =>
            configuration is SmtpEmailConfiguration smtp
                ? new SmtpSettingsResponse(
                    smtp.SmtpServer,
                    smtp.Port,
                    smtp.Ssl is { Enabled: true },
                    smtp.From.GetOrElse(string.Empty),
                    smtp.Authentication.Match(() => NoAuthentication.TypeName, authentication => authentication.Type),
                    smtp.Authentication.Match(() => string.Empty, authentication => authentication.Username),
                    smtp.Authentication.Match(() => string.Empty, authentication => authentication.Password.MaskSensitiveValue()),
                    smtp.Templates.Match(
                        () => new Dictionary<string, string>(),
                        templates => templates.ToDictionary(template => template.Name, template => template.Content)))
                : SmtpSettingsResponse.Default;
    }

    extension(SmtpSettingsRequest smtp)
    {
        private Option<EmailConfiguration> ToConfiguration(string storedPassword) =>
            smtp.IsValid
                ? smtp.ToAuthentication(storedPassword).Map(authentication => (EmailConfiguration)new SmtpEmailConfiguration(
                    smtp.Server.Trim(),
                    smtp.Port,
                    new Ssl(smtp.Ssl),
                    Some(authentication),
                    smtp.ToTemplates(),
                    string.IsNullOrWhiteSpace(smtp.From) ? None : Some(smtp.From.Trim())))
                : None;

        private bool IsValid =>
            !string.IsNullOrWhiteSpace(smtp.Server)
            && smtp.Port is > 0 and <= MaxPort
            && (string.IsNullOrWhiteSpace(smtp.From) || MailAddress.TryCreate(smtp.From.Trim(), out _))
            && (smtp.Templates ?? NoTemplates).Keys.All(name => TemplateNames.Contains(name));

        private Option<Authentication> ToAuthentication(string storedPassword) =>
            smtp.Authentication switch
            {
                NoAuthentication.TypeName => Some<Authentication>(new NoAuthentication()),
                BasicAuthentication.TypeName when !string.IsNullOrWhiteSpace(smtp.Username)
                    && !string.IsNullOrEmpty(smtp.Password.OrElse(storedPassword)) =>
                    Some<Authentication>(new BasicAuthentication(smtp.Username.Trim(), smtp.Password.OrElse(storedPassword))),
                _ => None
            };

        private Option<Templates> ToTemplates()
        {
            IReadOnlyDictionary<string, string?> values = (smtp.Templates ?? NoTemplates)
                .ToDictionary(pair => pair.Key, pair => (string?)pair.Value);
            var templates = (Templates)new TemplatesTypeConverter().ConvertFrom(values)!;

            return templates.Any() ? Some(templates) : None;
        }
    }

    extension(string? value)
    {
        private string OrElse(string fallback) => string.IsNullOrEmpty(value) ? fallback : value;
    }
}
