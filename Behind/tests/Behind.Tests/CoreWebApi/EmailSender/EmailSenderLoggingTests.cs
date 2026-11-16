using Behind.Core.WebApi.EmailSender;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreWebApi.EmailSender;

public class EmailSenderLoggingTests
{
    [Fact]
    public async Task LoggerEmailSender_LogsStructuredPascalCaseTemplateWithoutTheBody()
    {
        var logger = new CapturingLogger<LoggerEmailSender>();

        await new LoggerEmailSender(logger, FakeHostEnvironment.Production).SendEmailAsync("user@test.com", "Hello", "<p>secret-link</p>");

        var entry = logger.Single(LogLevel.Information);
        Assert.Equal("user@test.com", entry.State["Email"]);
        Assert.Equal("Hello", entry.State["Subject"]);
        Assert.DoesNotContain("secret-link", entry.Message);
        Assert.DoesNotContain(entry.State.Values, value => value?.ToString()?.Contains("secret-link") == true);
        Assert.DoesNotContain(entry.State.Keys, key => char.IsLower(key[0]));
    }
}
