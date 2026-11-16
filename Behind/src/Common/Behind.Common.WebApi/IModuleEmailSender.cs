namespace Behind.Common.WebApi;

public interface IModuleEmailSender
{
    Task SendEmailAsync(string email, string subject, string htmlMessage, CancellationToken cancellationToken);
}
