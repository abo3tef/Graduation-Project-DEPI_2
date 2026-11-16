using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Medilog.Infrastructure.Security;

/// <summary>
/// Validates Firebase ID tokens with the framework JWT handler (F6-C1-T8, decision Q-12).
/// </summary>
/// <remarks>
/// <para>
/// The token must be signed by Google, carry the configured project as both issuer
/// (<c>https://securetoken.google.com/{projectId}</c>) and audience, be unexpired and have a subject.
/// Google's public signing certificates are fetched once and cached with the lifetime the endpoint
/// advertises; a key id that is not cached triggers one refresh so key rotation keeps working.
/// </para>
/// <para>
/// No Firebase SDK is used (decision D-41). Only the token hash or key id is ever logged: the ID
/// token is a credential and is never written to logs or returned (AGENTS.md §5).
/// </para>
/// </remarks>
public sealed class FirebaseGoogleTokenValidator : IGoogleTokenValidator, IDisposable
{
    /// <summary>Google's public certificates for Firebase ID tokens.</summary>
    internal const string CertificateUrl =
        "https://www.googleapis.com/robot/v1/metadata/x509/securetoken@system.gserviceaccount.com";

    /// <summary>Guards against a client forcing a download on every request with a bogus key id.</summary>
    private static readonly TimeSpan MinimumRefreshInterval = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan DefaultCacheLifetime = TimeSpan.FromHours(1);

    private readonly FirebaseOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<FirebaseGoogleTokenValidator> _logger;
    private readonly HttpClient _httpClient;
    private readonly JsonWebTokenHandler _handler = new();
    private readonly SemaphoreSlim _keyLock = new(1, 1);
    private readonly Dictionary<string, (SecurityKey Key, DateTimeOffset ValidUntil)> _keys = [];

    private DateTimeOffset _lastRefreshAt = DateTimeOffset.MinValue;

    public FirebaseGoogleTokenValidator(
        FirebaseOptions options,
        TimeProvider clock,
        ILogger<FirebaseGoogleTokenValidator> logger)
    {
        _options = options;
        _clock = clock;
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<GoogleTokenIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            throw new NotConfiguredException(
                "Google sign-in is not configured. Set Firebase:ProjectId.",
                ErrorCodes.AuthGoogleNotConfigured);
        }

        var keyId = ReadKeyId(idToken);
        var signingKey = await GetSigningKeyAsync(keyId, cancellationToken);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://securetoken.google.com/{_options.ProjectId}",
            ValidateAudience = true,
            ValidAudience = _options.ProjectId,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        };

        TokenValidationResult result;

        try
        {
            result = await _handler.ValidateTokenAsync(idToken, parameters);
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            throw InvalidGoogleToken();
        }

        if (!result.IsValid || result.ClaimsIdentity is null)
        {
            throw InvalidGoogleToken();
        }

        var subject = result.ClaimsIdentity.FindFirst("sub")?.Value;
        var email = result.ClaimsIdentity.FindFirst("email")?.Value;

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
        {
            throw InvalidGoogleToken();
        }

        return new GoogleTokenIdentity(
            subject,
            email,
            ReadEmailVerified(result.ClaimsIdentity));
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _keyLock.Dispose();
    }

    /// <summary>Reads the key id from the token header without validating the signature.</summary>
    private static string ReadKeyId(string idToken)
    {
        JsonWebToken token;

        try
        {
            token = new JsonWebToken(idToken);
        }
        catch (Exception exception) when (exception is ArgumentException or SecurityTokenException)
        {
            throw InvalidGoogleToken();
        }

        if (string.IsNullOrWhiteSpace(token.Kid))
        {
            throw InvalidGoogleToken();
        }

        return token.Kid;
    }

    private static bool ReadEmailVerified(System.Security.Claims.ClaimsIdentity identity)
    {
        var claim = identity.FindFirst("email_verified")?.Value;

        // The claim may arrive as JSON true, "true" or "True" depending on the serializer.
        return bool.TryParse(claim, out var verified) && verified;
    }

    private async Task<SecurityKey> GetSigningKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        if (TryGetCachedKey(keyId, out var cached))
        {
            return cached;
        }

        await _keyLock.WaitAsync(cancellationToken);

        try
        {
            if (TryGetCachedKey(keyId, out cached))
            {
                return cached;
            }

            var now = _clock.GetUtcNow();

            if (now - _lastRefreshAt >= MinimumRefreshInterval)
            {
                await RefreshKeysAsync(now, cancellationToken);
            }

            if (TryGetCachedKey(keyId, out cached))
            {
                return cached;
            }

            // An unknown key id is not a configuration problem: the token was not signed by
            // Google for this project.
            throw InvalidGoogleToken();
        }
        finally
        {
            _keyLock.Release();
        }
    }

    private bool TryGetCachedKey(string keyId, out SecurityKey key)
    {
        if (_keys.TryGetValue(keyId, out var entry) && entry.ValidUntil > _clock.GetUtcNow())
        {
            key = entry.Key;
            return true;
        }

        key = null!;
        return false;
    }

    private async Task RefreshKeysAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await _httpClient.GetAsync(CertificateUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(exception, "Google signing certificates could not be retrieved.");

            throw new NotConfiguredException(
                "Google sign-in is temporarily unavailable.",
                ErrorCodes.AuthGoogleNotConfigured);
        }

        using (response)
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var validUntil = now + (response.Headers.CacheControl?.MaxAge ?? DefaultCacheLifetime);

            using var document = JsonDocument.Parse(json);

            _keys.Clear();

            foreach (var property in document.RootElement.EnumerateObject())
            {
                var certificate = property.Value.GetString();

                if (string.IsNullOrWhiteSpace(certificate))
                {
                    continue;
                }

                var x509 = X509Certificate2.CreateFromPem(certificate.Trim());
                _keys[property.Name] = (new X509SecurityKey(x509) { KeyId = property.Name }, validUntil);
            }

            _lastRefreshAt = now;
        }
    }

    private static AuthenticationFailedException InvalidGoogleToken() =>
        new("The Google sign-in token is not valid.", ErrorCodes.AuthInvalidGoogleToken);
}
