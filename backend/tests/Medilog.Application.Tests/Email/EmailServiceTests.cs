using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Medilog.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;

namespace Medilog.Application.Tests.Email;

/// <summary>
/// Transactional-email behaviour (F6-C1-T9, decisions D-39/D-41): the development file sink and the
/// refusal to pretend mail was delivered when SMTP is switched on but incomplete.
/// </summary>
public sealed class EmailServiceTests : IDisposable
{
    private const string To = "new.doctor@medilog.test";
    private const string Subject = "Verify your Medilog email address";
    private const string Body = "Open https://app.medilog.test/verify-email?token=abc123 to confirm.";

    private readonly string _sinkDirectory = Path.Combine(
        Path.GetTempPath(),
        $"medilog-dev-mail-{Guid.NewGuid():N}");

    [Fact]
    public async Task Disabled_smtp_writes_the_message_to_the_development_sink()
    {
        // Arrange: Email:Enabled is false, so nothing may leave the machine (D-39).
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        var service = CreateService(new EmailOptions
        {
            Enabled = false,
            DevMailDirectory = _sinkDirectory,
        }, clock);

        // Act
        await service.SendAsync(new EmailMessage(To, Subject, Body), CancellationToken.None);

        // Assert
        var file = Assert.Single(Directory.GetFiles(_sinkDirectory, "*.eml"));
        var content = await File.ReadAllTextAsync(file);

        Assert.Contains($"To: {To}", content, StringComparison.Ordinal);
        Assert.Contains($"Subject: {Subject}", content, StringComparison.Ordinal);
        Assert.Contains(Body, content, StringComparison.Ordinal);
        Assert.Contains(clock.GetUtcNow().ToString("r"), content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Enabled_smtp_without_a_host_fails_without_writing_a_file()
    {
        // Arrange: Enabled means mail must really be sent, so an incomplete configuration is an
        // error, not a silent fallback to the sink.
        var service = CreateService(new EmailOptions
        {
            Enabled = true,
            Host = string.Empty,
            DevMailDirectory = _sinkDirectory,
        }, new FixedClock(DateTimeOffset.UtcNow));

        // Act
        var failure = await Assert.ThrowsAsync<NotConfiguredException>(
            () => service.SendAsync(new EmailMessage(To, Subject, Body), CancellationToken.None));

        // Assert
        Assert.Equal(ErrorCodes.AuthNotConfigured, failure.Code);
        Assert.False(Directory.Exists(_sinkDirectory));
    }

    [Fact]
    public async Task Every_message_gets_its_own_file_even_with_the_same_timestamp()
    {
        // Arrange
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        var service = CreateService(new EmailOptions
        {
            Enabled = false,
            DevMailDirectory = _sinkDirectory,
        }, clock);

        // Act
        await service.SendAsync(new EmailMessage(To, Subject, Body), CancellationToken.None);
        await service.SendAsync(new EmailMessage(To, Subject, Body), CancellationToken.None);

        // Assert
        Assert.Equal(2, Directory.GetFiles(_sinkDirectory, "*.eml").Length);
    }

    public void Dispose()
    {
        if (Directory.Exists(_sinkDirectory))
        {
            Directory.Delete(_sinkDirectory, recursive: true);
        }
    }

    private static EmailService CreateService(EmailOptions options, TimeProvider clock) =>
        new(options, clock, NullLogger<EmailService>.Instance);

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedClock(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
