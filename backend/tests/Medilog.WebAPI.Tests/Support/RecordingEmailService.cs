using Medilog.Application.Common.Abstractions;

namespace Medilog.WebAPI.Tests.Support;

/// <summary>
/// Captures transactional email in memory instead of sending it, so HTTP tests can read the
/// one-time link the user would receive without an SMTP server (F6-C1).
/// </summary>
/// <remarks>
/// Registered last through <see cref="WebApiFactory"/> and accessed from the test thread only; the
/// message bodies contain one-time tokens and are never written to logs (AGENTS.md §5).
/// </remarks>
internal sealed class RecordingEmailService : IEmailService
{
    private readonly List<EmailMessage> _messages = [];

    public IReadOnlyList<EmailMessage> Messages
    {
        get
        {
            lock (_messages)
            {
                return _messages.ToArray();
            }
        }
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        lock (_messages)
        {
            _messages.Add(message);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Reads the raw one-time token out of an emailed link.</summary>
internal static class AuthEmailReader
{
    public static string ReadToken(EmailMessage message)
    {
        var link = message.Body
            .Split('\n', StringSplitOptions.TrimEntries)
            .First(line => line.StartsWith("http", StringComparison.Ordinal));

        return Uri.UnescapeDataString(
            new Uri(link).Query.Replace("?token=", string.Empty, StringComparison.Ordinal));
    }
}
