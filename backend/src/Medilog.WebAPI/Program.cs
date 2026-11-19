using System.Text;
using System.Text.Json.Serialization;
using Medilog.Application;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Interfaces;
using Medilog.Infrastructure;
using Medilog.Infrastructure.Persistence;
using Medilog.Infrastructure.Persistence.Seed;
using Medilog.Infrastructure.Security;
using Medilog.WebAPI.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Layer registrations (once).
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);

// The acting user is resolved server-side from the validated bearer principal only
// (decision D-11). Client-supplied identity headers are never trusted.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Bearer authentication for the HS256 tokens issued by /api/auth (decisions D-35/D-36).
// Validation parameters are built from the same JwtOptions the issuer uses, at options-build
// time, so configuration overrides (user-secrets, environment, test hosts) are honored.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtOptions>((options, jwtOptions) =>
    {
        // Claims keep their wire names (sub/role/doctorId), which is exactly what the claims
        // contract and CurrentUser read. Role checks use "role" as the role claim type.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtOptions.IsConfigured
                ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
                : null,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            NameClaimType = "sub",
            RoleClaimType = "role",
        };
    });
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

// Transactional email is required for registration and password recovery. Development and the
// test host may use the file sink; every real environment must have SMTP configured, otherwise an
// email would be silently written to disk instead of delivered (decision D-39).
if (!app.Environment.IsDevelopment() &&
    !app.Environment.IsEnvironment("Testing") &&
    !app.Configuration.GetValue<bool>("Email:Enabled"))
{
    throw new InvalidOperationException(
        "Email is disabled. Set Email:Enabled=true and configure Email:Host, Email:Port and " +
        "Email:FromAddress outside Development.");
}

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

// Apply the checked-in EF Core migrations before any startup database work. The initializer
// logs a clear, actionable error on failure and keeps the development seeder from querying
// tables that do not yet exist.
using (var migrationScope = app.Services.CreateScope())
{
    await migrationScope.ServiceProvider.GetRequiredService<DatabaseInitializer>()
        .InitializeAsync(app.Lifetime.ApplicationStopping);
}

// Development-only demo accounts (decision D-40). Gated on both the environment and the explicit
// Seed:Enabled switch, so production and test hosts never touch the database here.
if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Seed:Enabled"))
{
    using var seedScope = app.Services.CreateScope();
    await seedScope.ServiceProvider.GetRequiredService<DevelopmentSeeder>().SeedAsync();
}

app.Run();

// Makes the entry point visible to WebApplicationFactory.
public partial class Program;
