namespace Medilog.Application.Common.Abstractions;

/// <summary>
/// One plain-text transactional email (F6-C1-T9).
/// </summary>
/// <param name="To">Recipient address.</param>
/// <param name="Subject">Short subject line.</param>
/// <param name="Body">Plain-text body. May contain a one-time link but never a password.</param>
/// <remarks>
/// Emails carry verification/reset links whose token is single-use and expiring (decision D-33).
/// They are never logged: the body is handled only by the email implementation (AGENTS.md §5).
/// </remarks>
public sealed record EmailMessage(string To, string Subject, string Body);

/// <summary>
/// Sends transactional email.
/// </summary>
/// <remarks>
/// Implemented in Infrastructure with the BCL SMTP client. When SMTP is not configured the
/// Development implementation writes the message to a local directory instead of dropping it
/// (decision D-39); outside Development a missing configuration is a startup error.
/// </remarks>
public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
