using System.Diagnostics.CodeAnalysis;
using Behind.Common.Templating;

namespace Behind.Core.WebApi.EmailSender;

[ExcludeFromCodeCoverage]
public static class DefaultEmailTemplates
{
    public static readonly SimpleTemplate ConfirmationEmail = new("ConfirmationEmail", "<p>Please confirm your email ({{Email}}) by clicking <a href=\"{{ConfirmationLink}}\">here</a>.</p>"); 

    public static readonly SimpleTemplate PasswordResetCodeEmail = new("PasswordResetCodeEmail", "<p>Your password reset code for email ({{Email}}) is: <b>{{ResetCode}}</b></p>");

    public static readonly SimpleTemplate PasswordResetLinkEmail = new("PasswordResetLinkEmail", "<p>Please reset your password for email ({{Email}}) by clicking <a href=\"{{ResetLink}}\">here</a>.</p>");
}