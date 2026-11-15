using Medilog.Application.Common.Exceptions;
using Microsoft.Extensions.Configuration;

namespace Medilog.Infrastructure.Email;

/// <summary>
/// Configuration for transactional email (F6-C1-T9, decisions D-39/D-41).
/// </summary>
/// <remarks>
/// <para>
/// With <see cref="Enabled"/> false the message is written to <see cref="DevMailDirectory"/> instead
/// of being sent, so the demo works without SMTP credentials (D-39). The WebAPI host refuses to
/// start with email disabled outside Development, so this can never silently drop real mail.
/// </para>
/// <para>
/// Host credentials and the SMTP password are secrets and come from user-secrets or environment
/// variables; they are never committed (AGENTS.md §5). The section is read manually so no
/// additional configuration package is required (D-41).
/// </para>
/// </remarks>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    private const int DefaultPort = 587;
    private const string DefaultFromAddress = "no-reply@medilog.local";
    private const string DefaultFromName = "Medilog";
    private const string DefaultDevMailDirectory = "logs/dev-mail";

    /// <summary>When false, mail is written to the development sink instead of sent over SMTP.</summary>
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = DefaultPort;

    /// <summary>STARTTLS over the configured port; SMTP credentials are required by most providers.</summary>
    public bool UseStartTls { get; set; } = true;

    public string UserName { get; set; } = string.Empty;

    /// <summary>SMTP password. Secret; never logged and never returned by an endpoint.</summary>
    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = DefaultFromAddress;

    public string FromName { get; set; } = DefaultFromName;

    /// <summary>Absolute path where development messages are written (gitignored).</summary>
    public string DevMailDirectory { get; set; } = DefaultDevMailDirectory;

    /// <summary>True when SMTP has enough configuration to attempt a send.</summary>
    public bool IsSmtpConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        Port > 0 &&
        !string.IsNullOrWhiteSpace(FromAddress);

    /// <summary>Reads the <c>Email</c> section and resolves the development sink path.</summary>
    public static EmailOptions FromConfiguration(IConfiguration configuration, string? contentRootPath)
    {
        var section = configuration.GetSection(SectionName);

        var devMailDirectory = section["DevMailDirectory"];

        if (string.IsNullOrWhiteSpace(devMailDirectory))
        {
            devMailDirectory = DefaultDevMailDirectory;
        }

        var mailDirectory = Path.IsPathRooted(devMailDirectory)
            ? devMailDirectory
            : Path.GetFullPath(devMailDirectory, contentRootPath ?? AppContext.BaseDirectory);

        return new EmailOptions
        {
            Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
            Host = section["Host"] ?? string.Empty,
            Port = int.TryParse(section["Port"], out var port) ? port : DefaultPort,
            UseStartTls = !bool.TryParse(section["UseStartTls"], out var useStartTls) || useStartTls,
            UserName = section["UserName"] ?? string.Empty,
            Password = section["Password"] ?? string.Empty,
            FromAddress = section["FromAddress"] is { Length: > 0 } fromAddress ? fromAddress : DefaultFromAddress,
            FromName = section["FromName"] is { Length: > 0 } fromName ? fromName : DefaultFromName,
            DevMailDirectory = mailDirectory,
        };
    }

    /// <summary>
    /// Fails when the section cannot deliver mail in the current mode.
    /// </summary>
    /// <exception cref="NotConfiguredException">SMTP is enabled but incomplete.</exception>
    public void EnsureCanSend()
    {
        if (Enabled && !IsSmtpConfigured)
        {
            throw new NotConfiguredException(
                "Email is not configured. Set Email:Host, Email:Port and Email:FromAddress, or disable SMTP in Development.",
                ErrorCodes.AuthNotConfigured);
        }
    }
}
