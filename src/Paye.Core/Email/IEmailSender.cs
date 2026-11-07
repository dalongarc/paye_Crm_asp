namespace Paye.Core.Email;

public sealed record EmailSendResult(bool Succeeded, bool NotConfigured, string? ErrorMessage = null);

public interface IEmailSender
{
    bool IsConfigured { get; }

    Task<EmailSendResult> SendEmailAsync(
        string toEmail,
        string subject,
        string plainTextBody,
        CancellationToken cancellationToken = default);
}
