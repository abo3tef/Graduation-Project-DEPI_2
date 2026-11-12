using System.Text.Json.Serialization;
using Medilog.Application;
using Medilog.Application.Interfaces;
using Medilog.Infrastructure;
using Medilog.WebAPI.Middleware;
using Medilog.WebAPI.Security;

var builder = WebApplication.CreateBuilder(args);

// Layer registrations
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Temporary identity source (Q-07): reads X-User-Id / X-User-Role headers, and only
// when Auth:AllowHeaderIdentity is true (Development). Replace with the real
// authenticated-user implementation when authentication is delivered.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HeaderCurrentUser>();

// API services. Enums are written and read as their names ("NoShow"), never numbers;
// dates are yyyy-MM-dd and times HH:mm:ss (System.Text.Json defaults).
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddOpenApi();

// One place that maps exceptions to ProblemDetails responses.
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// Consistent, client-readable error responses (AGENTS.md §3.2, §4.4).
// Uses the framework-standard RFC 7807 ProblemDetails shape; the final shared
// error envelope is still pending decision Q-04 in STATE.md.
builder.Services.AddProblemDetails();

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

// HTTP request pipeline
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthorization();

app.MapControllers();

app.Run();

// Makes the entry point visible to WebApplicationFactory in Medilog.WebAPI.Tests.
public partial class Program;
