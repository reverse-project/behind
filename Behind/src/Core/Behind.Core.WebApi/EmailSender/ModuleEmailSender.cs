using Behind.Common.WebApi;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace Behind.Core.WebApi.EmailSender;

public sealed class ModuleEmailSender(IEmailSender sender) : IModuleEmailSender
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return sender is EmailSender deliverySender
            ? deliverySender.SendDeliveryEmailAsync(email, subject, htmlMessage, cancellationToken)
            : sender.SendEmailAsync(email, subject, htmlMessage).WaitAsync(cancellationToken);
    }
}
