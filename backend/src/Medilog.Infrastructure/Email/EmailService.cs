using System.Net;
using System.Net.Mail;
using System.Text;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;

namespace Medilog.Infrastructure.Email;

/// <summary>
/// Sends transactional email over SMTP, or writes it to the development sink when SMTP is disabled
/// (F6-C1-T9, decisions D-39/D-41).
/// </summary>
/// <remarks>
/// <para>
/// Only the BCL is used: <see cref="SmtpClient"/> for delivery and a plain <c>.eml</c> file for the
/// development sink, so no mail package is added (D-41).
/// </para>
/// <para>
/// With SMTP enabled the message body is never logged, and SMTP failures are rethrown with a
/// generic <c>503 auth.not_configured</c> style failure instead of the SMTP detail. The development
/// sink is only reachable in the Development environment (the host refuses a disabled email service
/// elsewhere), so it intentionally writes the full message - including the one-time verification or
/// reset link - to the console as well as to the <c>.eml</c> file. That makes the flow testable
/// without SMTP credentials; no token ever reaches a log outside Development.
/// </para>
/// </remarks>
public sealed class EmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<EmailService> _logger;

    public EmailService(EmailOptions options, TimeProvider clock, ILogger<EmailService> logger)
    {
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _options.EnsureCanSend();

        if (_options.Enabled)
        {
            await SendOverSmtpAsync(message, cancellationToken);
            return;
        }

        await WriteToDevelopmentSinkAsync(message, cancellationToken);
    }

    private async Task SendOverSmtpAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        using var mail = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = false,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8,
        };

        mail.To.Add(message.To);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseStartTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = string.IsNullOrWhiteSpace(_options.UserName)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_options.UserName, _options.Password),
        };

        try
        {
            await client.SendMailAsync(mail, cancellationToken);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException)
        {
            // The SMTP detail can name internal hosts and accounts, so it is not returned to the
            // client and not logged with the message body (AGENTS.md §5).
            _logger.LogError(exception, "Sending a transactional email failed.");

            throw new NotConfiguredException(
                "Email could not be sent. Try again later.",
                ErrorCodes.AuthNotConfigured);
        }
    }

    private async Task WriteToDevelopmentSinkAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.DevMailDirectory);

        var fileName = string.Concat(
            _clock.GetUtcNow().ToString("yyyyMMdd-HHmmssfff"),
            "-",
            Guid.NewGuid().ToString("N"),
            ".eml");

        var path = Path.Combine(_options.DevMailDirectory, fileName);
        var content = BuildMimeMessage(message);

        await File.WriteAllTextAsync(path, content, Encoding.UTF8, cancellationToken);

        // Development only: show the real link so the registration -> verification flow can be
        // exercised without an SMTP server. Program.cs refuses this sink outside Development.
        _logger.LogInformation(
            "SMTP is disabled, so a development email was written to {File}.\n" +
            "To: {Recipient}\nSubject: {Subject}\n{Body}",
            fileName,
            message.To,
            message.Subject,
            message.Body);
    }

    private string BuildMimeMessage(EmailMessage message) =>
        string.Join(
            "\r\n",
            "From: " + new MailAddress(_options.FromAddress, _options.FromName).ToString(),
            "To: " + message.To,
            "Subject: " + message.Subject,
            "Date: " + _clock.GetUtcNow().ToString("r"),
            "MIME-Version: 1.0",
            "Content-Type: text/plain; charset=utf-8",
            "Content-Transfer-Encoding: 8bit",
            string.Empty,
            message.Body);
}
