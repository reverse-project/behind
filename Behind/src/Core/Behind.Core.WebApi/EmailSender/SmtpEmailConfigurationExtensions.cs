using Behind.Common;
using Behind.Core.Common.Configuration;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.EmailSender;

public static class SmtpEmailConfigurationExtensions
{
    extension(SmtpEmailConfiguration configuration)
    {
        public Option<string> SenderAddress =>
            configuration.From.Match<Option<string>>(
                none: () => configuration.Authentication.Bind<Authentication, string>(
                    authentication => authentication is BasicAuthentication basic
                        ? Some(basic.Username)
                        : None),
                some: address => address);
    }
}
