using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paye.Core.Configuration;
using Paye.Core.Email;

namespace Paye.Infrastructure.Email;

public sealed class SmtpEmailSender(
    IOptions<SmtpSettings> smtpOptions,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpSettings _settings = smtpOptions.Value;

    public bool IsConfigured => _settings.IsConfigured;

    public async Task<EmailSendResult> SendEmailAsync(
        string toEmail,
        string subject,
        string plainTextBody,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            logger.LogWarning(
                "Intento de envío de correo abortado porque el proveedor SMTP no está configurado (Smtp:Host / Smtp:FromEmail vacíos).");

            return new EmailSendResult(
                Succeeded: false,
                NotConfigured: true,
                ErrorMessage: "El proveedor de correo SMTP no está configurado en este servidor. Contacte con el propietario para restablecer su contraseña.");
        }

        try
        {
            using var client = new SmtpClient(_settings.Host!, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(_settings.Username) &&
                !string.IsNullOrWhiteSpace(_settings.Password))
            {
                client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
            }

            using var message = new MailMessage
            {
                From = new MailAddress(_settings.FromEmail!, _settings.FromName),
                Subject = subject,
                Body = plainTextBody,
                IsBodyHtml = false
            };
            message.To.Add(new MailAddress(toEmail));

            await client.SendMailAsync(message, cancellationToken);
            return new EmailSendResult(Succeeded: true, NotConfigured: false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al enviar correo mediante el servidor SMTP configurado.");
            return new EmailSendResult(
                Succeeded: false,
                NotConfigured: false,
                ErrorMessage: "No se pudo enviar el correo en este momento.");
        }
    }
}
