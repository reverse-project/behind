using System.Diagnostics.CodeAnalysis;
using Behind.Common.Templating;

namespace Behind.Core.WebApi.EmailSender;

[ExcludeFromCodeCoverage]
public static class DefaultEmailTemplates
{
    public static readonly SimpleTemplate ConfirmationEmail = new("ConfirmationEmail", "<p>Please confirm your email ({{Email}}) by clicking <a href=\"{{ConfirmationLink}}\">here</a>.</p>");

    public static readonly SimpleTemplate PasswordResetCodeEmail = new("PasswordResetCodeEmail", "<p>Your password reset code for email ({{Email}}) is: <b>{{ResetCode}}</b></p>");

    public static readonly SimpleTemplate PasswordResetLinkEmail = new("PasswordResetLinkEmail", "<p>Please reset your password for email ({{Email}}) by clicking <a href=\"{{ResetLink}}\">here</a>.</p>");

    public static readonly SimpleTemplate MagicLinkEmail = new("MagicLinkEmail", "<p>Sign in with email ({{Email}}) by clicking <a href=\"{{MagicLink}}\">here</a>. If you did not request this, you can ignore this message.</p>");

    internal static readonly SimpleTemplate EmailSignInAlert = new("EmailSignInAlert",
        "<p>A new sign-in to your account succeeded.</p><p>Remote IP: {{RemoteIp}}<br>User agent: {{UserAgent}}</p><p>If you did not sign in, review your active sessions and secure your account.</p>");

    public static readonly IReadOnlyList<SimpleTemplate> All =
        [ConfirmationEmail, PasswordResetCodeEmail, PasswordResetLinkEmail, MagicLinkEmail];
}
