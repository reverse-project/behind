using System.Text;
using System.Text.Json;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public sealed class ProfileEmailsFrontendTests
{
    private static readonly IFileProvider Provider = EmbeddedFrontend.CreateFileProvider();

    private static string Read(string path)
    {
        using var reader = new StreamReader(Provider.GetFileInfo(path).CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Profile_OffersIndependentEmailEditingConfirmationResendAndGuardedRemoval()
    {
        var view = Read("views/profile.html");
        Assert.Contains("foreach: emails", view);
        Assert.Contains("value: draft", view);
        Assert.Contains("value: draft, valueUpdate: 'input'", view);
        Assert.Contains("value: newEmail, valueUpdate: 'input'", view);
        Assert.Contains("type=\"email\"", view);
        Assert.Contains("click: $parent.resendEmail", view);
        Assert.Contains("$parent.emails().length > 1", view);
        Assert.Contains("enable: canManageEmails", view);
        Assert.Contains("models/profileemails", Read("viewmodels/profile.js"));
        Assert.Contains("http.get(\"/profile/emails\")", Read("models/profileemails.js"));
        Assert.DoesNotContain("html:", view);
    }

    [Fact]
    public void Confirmation_PreservesTheAddressIdFromLinkToRequest()
    {
        Assert.Contains("context.params.emailId", Read("viewmodels/confirmemail.js"));
        Assert.Contains("emailId: emailId", Read("models/auth.js"));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("hu-HU")]
    public void EveryEmailMessage_IsLocalized(string locale)
    {
        using var document = JsonDocument.Parse(Read($"locales/{locale}.json"));
        var messages = document.RootElement.GetProperty("messages");
        foreach (var key in new[] { "emails", "emailsHelp", "emailReconfirmationHelp", "emailNew", "emailAdd",
                     "emailConfirmed", "emailUnconfirmed", "emailResend", "emailSaved", "emailRemoved",
                     "emailConfirmationSent", "emailSavedSendFailed", "emailSendFailed", "emailInvalid",
                     "emailInUse", "emailLast", "emailNotFound", "emailImpersonated", "emailRemoveConfirm" })
        {
            Assert.False(string.IsNullOrWhiteSpace(messages.GetProperty("profile." + key).GetString()));
        }
    }
}
