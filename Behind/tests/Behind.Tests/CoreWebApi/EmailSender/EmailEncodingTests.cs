using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace Behind.Tests.CoreWebApi.EmailSender;

public sealed class EmailEncodingTests
{
    [Theory]
    [InlineData("confirmation")]
    [InlineData("reset")]
    [InlineData("magic")]
    [InlineData("code")]
    public async Task EncodesUntrustedTemplateParameters(string kind)
    {
        var sender = new CapturingSender();
        var email = "\"<a href='https://evil.test'>click</a>\" <user@test.com>";
        var user = new ApplicationUser();
        await (kind switch
        {
            "confirmation" => sender.SendConfirmationLinkAsync(user, email, "https://test/?a=1&b=2"),
            "reset" => sender.SendPasswordResetLinkAsync(user, email, "https://test/?a=1&b=2"),
            "magic" => sender.SendMagicLinkAsync(user, email, "https://test/?a=1&b=2"),
            _ => sender.SendPasswordResetCodeAsync(user, email, "<script>")
        });
        Assert.DoesNotContain("<a href='https://evil.test'>", sender.Body);
        Assert.Contains("&lt;a", sender.Body);
        Assert.DoesNotContain("<script>", sender.Body);
    }

    private sealed class CapturingSender : Behind.Core.WebApi.EmailSender.EmailSender
    {
        public string Body { get; private set; } = "";
        public override Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            Body = htmlMessage;
            return Task.CompletedTask;
        }
    }
}
