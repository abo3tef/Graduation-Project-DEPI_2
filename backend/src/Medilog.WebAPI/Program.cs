using Medilog.Application;
using Medilog.Application.Common.Abstractions;
using Medilog.Infrastructure;
using Medilog.WebAPI.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// Layer registrations
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Acting identity and HTTP context (D-11/D-16). No stub is registered: the doctor always comes
// from the validated token, never from a request body.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Authentication validates JWT bearer tokens only. Issuing tokens (login) is outside the
// five-feature scope; local development uses 'dotnet user-jwts' (D-16).
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddAuthorization();

// API services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Consistent, client-readable error responses (AGENTS.md §3.2, §4.4).
// Uses the framework-standard RFC 7807 ProblemDetails shape; the final shared
// error envelope is still pending decision Q-04 in STATE.md.
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

// HTTP request pipeline
app.UseExceptionHandler();

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

// Exposes the implicit entry point to WebApplicationFactory<Program> in the integration tests.
public partial class Program
{
}
