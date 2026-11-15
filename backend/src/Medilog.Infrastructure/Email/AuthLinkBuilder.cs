using Medilog.Application.Common.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Medilog.Infrastructure.Email;

/// <summary>
/// Configuration for the links emailed by the authentication flows (F6-C1-T4/T6/T7).
/// </summary>
/// <remarks>
/// <c>Auth:AppBaseUrl</c> points at the frontend that hosts <c>/verify-email</c> and
/// <c>/reset-password</c>. It is ordinary configuration, not a secret.
/// </remarks>
public sealed class AuthLinkOptions
{
    public const string SectionName = "Auth";

    public string AppBaseUrl { get; set; } = "http://localhost:3000";

    public static AuthLinkOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);

        return new AuthLinkOptions
        {
            AppBaseUrl = section["AppBaseUrl"] is { Length: > 0 } baseUrl
                ? baseUrl
                : "http://localhost:3000",
        };
    }
}

/// <summary>
/// Builds absolute frontend links for emailed one-time tokens.
/// </summary>
public sealed class AuthLinkBuilder : IAuthLinkBuilder
{
    private readonly string _baseUrl;

    public AuthLinkBuilder(AuthLinkOptions options)
    {
        _baseUrl = options.AppBaseUrl.TrimEnd('/');
    }

    public string BuildEmailVerificationLink(string token) => BuildLink("verify-email", token);

    public string BuildPasswordResetLink(string token) => BuildLink("reset-password", token);

    private string BuildLink(string path, string token) =>
        $"{_baseUrl}/{path}?token={Uri.EscapeDataString(token)}";
}
