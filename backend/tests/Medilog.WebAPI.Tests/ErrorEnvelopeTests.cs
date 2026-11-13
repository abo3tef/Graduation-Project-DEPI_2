using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Security;
using Medilog.WebAPI.Tests.Support;
using Xunit;

namespace Medilog.WebAPI.Tests;

/// <summary>
/// Pins the single API error envelope (Phase 1 / P1-C2): every error path returns
/// <c>application/problem+json</c> with <c>type</c>, <c>title</c>, <c>status</c>,
/// <c>instance</c>, <c>code</c> and <c>traceId</c>, and validation errors also carry
/// camelCase <c>errors</c> and <c>errorCodes</c>.
/// </summary>
public sealed class ErrorEnvelopeTests
{
    private const string VisitsUrl = "/api/medical-visits";

    [Fact]
    public async Task Validation_error_uses_the_shared_envelope_with_camel_case_fields()
    {
        using var factory = new WebApiFactory();
        using var client = factory.CreateClientFor(Guid.NewGuid());

        var response = await client.PostAsJsonAsync(VisitsUrl, ApiData.CreateVisit(Guid.Empty));

        var problem = await AssertEnvelopeAsync(
            response, HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed, VisitsUrl);

        Assert.True(problem.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("patientId", out var messages));
        Assert.Equal(JsonValueKind.Array, messages.ValueKind);

        Assert.True(problem.TryGetProperty("errorCodes", out var errorCodes));
        Assert.True(errorCodes.TryGetProperty("patientId", out var codes));
        Assert.Equal(JsonValueKind.Array, codes.ValueKind);
    }

    [Fact]
    public async Task Model_binding_error_uses_request_invalid()
    {
        using var factory = new WebApiFactory();
        using var client = factory.CreateClientFor(Guid.NewGuid());

        using var content = new StringContent(
            "{\"patientId\":\"not-a-guid\"}",
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync(VisitsUrl, content);

        var problem = await AssertEnvelopeAsync(
            response, HttpStatusCode.BadRequest, ErrorCodes.RequestInvalid, VisitsUrl);

        Assert.True(problem.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("patientId", out _));
    }

    [Fact]
    public async Task Unauthenticated_request_uses_the_shared_envelope()
    {
        using var factory = new WebApiFactory();
        using var client = factory.CreateClientFor(userId: null);

        var url = $"{VisitsUrl}/{Guid.NewGuid()}";
        var response = await client.GetAsync(url);

        await AssertEnvelopeAsync(
            response,
            HttpStatusCode.Unauthorized,
            ErrorCodes.Unauthorized,
            url);
    }

    [Fact]
    public async Task Forbidden_request_uses_the_shared_envelope()
    {
        using var factory = new WebApiFactory();
        using var client = factory.CreateClientFor(Guid.NewGuid(), Roles.Secretary);

        var url = $"{VisitsUrl}/{Guid.NewGuid()}";
        var response = await client.GetAsync(url);

        await AssertEnvelopeAsync(
            response,
            HttpStatusCode.Forbidden,
            ErrorCodes.Forbidden,
            url);
    }

    [Fact]
    public async Task Unmatched_route_uses_the_shared_envelope()
    {
        using var factory = new WebApiFactory();
        using var client = factory.CreateClientFor(Guid.NewGuid());

        var response = await client.GetAsync("/api/does-not-exist");

        await AssertEnvelopeAsync(
            response,
            HttpStatusCode.NotFound,
            ErrorCodes.NotFound,
            "/api/does-not-exist");
    }

    [Fact]
    public async Task Method_not_allowed_uses_the_shared_envelope()
    {
        using var factory = new WebApiFactory();
        using var client = factory.CreateClientFor(Guid.NewGuid());

        var url = $"{VisitsUrl}/{Guid.NewGuid()}";
        var response = await client.DeleteAsync(url);

        await AssertEnvelopeAsync(
            response,
            HttpStatusCode.MethodNotAllowed,
            ErrorCodes.MethodNotAllowed,
            url);
    }

    [Fact]
    public async Task Unsupported_media_type_uses_the_shared_envelope()
    {
        using var factory = new WebApiFactory();
        using var client = factory.CreateClientFor(Guid.NewGuid());

        using var content = new StringContent("{}", Encoding.UTF8, "text/plain");

        var response = await client.PostAsync(VisitsUrl, content);

        await AssertEnvelopeAsync(
            response,
            HttpStatusCode.UnsupportedMediaType,
            ErrorCodes.UnsupportedMediaType,
            VisitsUrl);
    }

    [Fact]
    public async Task Conflict_uses_the_shared_envelope()
    {
        await using var api = new ApiFactory();
        using var client = api.CreateClient();

        var first = await client.CallAsync(HttpMethod.Post, "/api/appointments", api.SecretaryCaller(), api.BookBody());
        var second = await client.CallAsync(HttpMethod.Post, "/api/appointments", api.SecretaryCaller(), api.BookBody());

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        await AssertEnvelopeAsync(
            second,
            HttpStatusCode.Conflict,
            ErrorCodes.AppointmentOverlap,
            "/api/appointments");
    }

    [Fact]
    public async Task Server_error_uses_the_shared_envelope_without_internal_details()
    {
        await using var api = new ApiFactory(clockOverride: new ThrowingClock());
        using var client = api.CreateClient();

        var response = await client.CallAsync(HttpMethod.Post, "/api/appointments", api.SecretaryCaller(), api.BookBody());

        var problem = await AssertEnvelopeAsync(
            response,
            HttpStatusCode.InternalServerError,
            ErrorCodes.ServerError,
            "/api/appointments");

        var raw = problem.GetRawText();
        Assert.DoesNotContain("hunter2", raw);
        Assert.DoesNotContain("SELECT", raw);
        Assert.DoesNotContain("InvalidOperationException", raw);
        Assert.DoesNotContain("   at ", raw);
    }

    private static async Task<JsonElement> AssertEnvelopeAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode,
        string expectedInstance)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.ReadJsonAsync();

        Assert.Equal((int)expectedStatus, json.GetProperty("status").GetInt32());
        Assert.Equal(expectedCode, json.GetProperty("code").GetString());
        Assert.Equal(expectedInstance, json.GetProperty("instance").GetString());

        var type = json.GetProperty("type").GetString();
        Assert.False(string.IsNullOrWhiteSpace(type));
        Assert.StartsWith("https://", type);

        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("traceId").GetString()));

        return json;
    }
}
