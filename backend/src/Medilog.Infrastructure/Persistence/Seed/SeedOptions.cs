using Microsoft.Extensions.Configuration;

namespace Medilog.Infrastructure.Persistence.Seed;

/// <summary>
/// Development seed-account settings (decision D-40).
/// </summary>
/// <remarks>
/// <para>
/// Seeding is off unless a host explicitly enables it, and the password is only used to create the
/// three demo accounts. It is a development credential, not a production secret; the real
/// protection is that the seeder never runs outside Development.
/// </para>
/// <para>
/// The section is read manually so no additional configuration package is required (D-41).
/// </para>
/// </remarks>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>When false (the default) the seeder does nothing at all.</summary>
    public bool Enabled { get; set; }

    /// <summary>Password shared by the three seeded demo accounts. Never used in production.</summary>
    public string Password { get; set; } = string.Empty;

    public static SeedOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);

        return new SeedOptions
        {
            Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
            Password = section["Password"] ?? string.Empty,
        };
    }
}
