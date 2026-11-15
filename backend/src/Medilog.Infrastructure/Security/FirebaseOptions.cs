using Microsoft.Extensions.Configuration;

namespace Medilog.Infrastructure.Security;

/// <summary>
/// Configuration for Google Sign-In (F6-C1-T8, decision Q-12).
/// </summary>
/// <remarks>
/// Only the Firebase project id is needed on the server: the ID token's issuer and audience are
/// both derived from it, and the public signing certificates are fetched from Google. The project id
/// is not a secret and is read from <c>Firebase:ProjectId</c>.
/// </remarks>
public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    public string ProjectId { get; set; } = string.Empty;

    /// <summary>True when a token can be validated at all.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ProjectId);

    public static FirebaseOptions FromConfiguration(IConfiguration configuration)
    {
        return new FirebaseOptions
        {
            ProjectId = configuration[$"{SectionName}:ProjectId"]?.Trim() ?? string.Empty,
        };
    }
}
