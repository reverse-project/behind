using Behind.Core.Domain;
using Behind.Core.WebApi.MagicLinks;

namespace Behind.Core.WebApi.EmailSender;

public static class MagicLinkEmailSenderExtensions
{
    extension(IMagicLinkEmailSender sender)
    {
        public Task Send(MagicLinkMail mail) =>
            mail.Function switch
            {
                MagicLinkFunction.LostPassword => sender.SendLostPasswordLinkAsync(mail.User, mail.Email, mail.Link),
                _ => sender.SendMagicLinkAsync(mail.User, mail.Email, mail.Link)
            };
    }
}
