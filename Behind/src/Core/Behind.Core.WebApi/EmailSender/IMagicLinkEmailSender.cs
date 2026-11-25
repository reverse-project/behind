using Behind.Core.WebApi.Identity;

namespace Behind.Core.WebApi.EmailSender;

public interface IMagicLinkEmailSender
{
    Task SendMagicLinkAsync(ApplicationUser user, string email, string magicLink);
    Task SendPinProofAsync(ApplicationUser user, string email, string link);
    Task SendLostPasswordLinkAsync(ApplicationUser user, string email, string magicLink);
}
