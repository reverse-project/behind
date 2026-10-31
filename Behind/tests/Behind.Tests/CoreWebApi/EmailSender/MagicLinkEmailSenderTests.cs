using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.EmailSender;
using Behind.Core.WebApi.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.EmailSender;

public class MagicLinkEmailSenderTests
{
    private const string Link = "https://example.com/sign/in/magiclink?token=abc";

    [Fact]
    public async Task LoggerEmailSender_SendMagicLinkAsync_RendersTemplateWithLinkAndEmail()
    {
        var logger = Substitute.For<ILogger<LoggerEmailSender>>();
        IMagicLinkEmailSender sender = new LoggerEmailSender(logger);

        await sender.SendMagicLinkAsync(new ApplicationUser { DisplayName = "User" }, "user@test.com", Link);

        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Your sign in link")
                                && o.ToString()!.Contains($"href=\"{Link}\"")
                                && o.ToString()!.Contains("user@test.com")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task SmtpEmailSender_SendMagicLinkAsync_UsesSubjectAndDefaultTemplate()
    {
        var logger = Substitute.For<ILogger<SmtpEmailSender>>();
        var configuration = new SmtpEmailConfiguration(
            "invalid.invalid",
            25,
            new Ssl(false),
            new NoAuthentication(),
            None);
        IMagicLinkEmailSender sender = new SmtpEmailSender(logger, Options.Create(configuration));

        await sender.SendMagicLinkAsync(new ApplicationUser { DisplayName = "User" }, "user@test.com", Link);

        logger.Received().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("user@test.com")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
