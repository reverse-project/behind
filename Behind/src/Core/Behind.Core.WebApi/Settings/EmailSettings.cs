using System.ComponentModel.DataAnnotations;
using Behind.Core.Common.Configuration;

namespace Behind.Core.WebApi.Settings;

public sealed record SmtpSettingsResponse(
    string Server,
    int Port,
    bool Ssl,
    string From,
    string Authentication,
    string Username,
    string PasswordMask,
    IReadOnlyDictionary<string, string> Templates)
{
    public const int DefaultPort = 587;

    public static readonly SmtpSettingsResponse Default = new(
        string.Empty,
        DefaultPort,
        true,
        string.Empty,
        NoAuthentication.TypeName,
        string.Empty,
        string.Empty,
        new Dictionary<string, string>());
}

public sealed record EmailSettingsResponse(
    string Provider,
    IReadOnlyList<string> Providers,
    IReadOnlyList<string> AuthenticationTypes,
    IReadOnlyDictionary<string, string> DefaultTemplates,
    SmtpSettingsResponse Smtp);

public sealed record SmtpSettingsRequest(
    string Server,
    int Port,
    bool Ssl,
    string? From,
    string Authentication,
    string? Username,
    string? Password,
    IReadOnlyDictionary<string, string>? Templates);

public sealed record EmailSettingsRequest([property: Required, MinLength(1)] string Provider, SmtpSettingsRequest? Smtp);
