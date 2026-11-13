using System.Text.Json.Serialization;
using Medilog.Application;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Interfaces;
using Medilog.Infrastructure;
using Medilog.WebAPI.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Layer registrations (once).
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// The acting user is resolved server-side from the authenticated principal
// (decision D-11). Until Q-07 lands, the implementation also accepts the
// X-User-Id / X-User-Role development headers when Auth:AllowHeaderIdentity is true.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Authentication must be registered so [Authorize] endpoints have a principal.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddAuthorization();

// API services. Enums are written and read as their names ("NoShow"), never numbers;
// dates are yyyy-MM-dd and times HH:mm:ss (System.Text.Json defaults).
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)))
    .ConfigureApiBehaviorOptions(options =>
    {
        // Client-error results (e.g. 415) are left body-less so UseStatusCodePages
        // emits the same envelope as every other error instead of MVC's default
        // ProblemDetails, which lacks code/traceId.
        options.SuppressMapClientErrors = true;

        // Model-binding failures (malformed JSON, wrong type, missing body) use the
        // same envelope as every other error, with the request.invalid code.
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value is { Errors.Count: > 0 })
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors
                        .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                            ? "The value is invalid."
                            : error.ErrorMessage)
                        .ToArray());

            var problem = ApiProblem.Validation(
                context.HttpContext,
                errors,
                errorCodes: null,
                ErrorCodes.RequestInvalid,
                "The request could not be read.");

            return ApiProblem.ToResult(problem);
        };
    });
builder.Services.AddOpenApi();

// One place that maps exceptions to RFC 7807 ProblemDetails responses.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? ["http://localhost:3000"];

        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Fail fast: a missing or unknown Clinic:TimeZone stops the application at startup
// instead of failing on the first appointment request.
_ = app.Services.GetRequiredService<IClinicClock>();

// HTTP request pipeline.
app.UseExceptionHandler();

// Framework-generated responses (401 challenge, 404 unmatched route, 405, 415,
// 5xx without a body) also carry the shared envelope. Placed early so the CORS
// middleware downstream can still add its headers before the body is written.
app.UseStatusCodePages(async statusCodeContext =>
{
    var httpContext = statusCodeContext.HttpContext;
    var problem = ApiProblem.FromStatusCode(httpContext, httpContext.Response.StatusCode);

    await ApiProblem.WriteAsync(httpContext, problem, httpContext.RequestAborted);
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

// Authentication must run before authorization so [Authorize(Roles = "Doctor")] sees a principal.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Makes the entry point visible to WebApplicationFactory.
public partial class Program;
