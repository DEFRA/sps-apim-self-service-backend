using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Scalar.AspNetCore;
using Sps.Apim.SelfServe.Infrastructure;
using Sps.Apim.SelfServe.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

var allowedFrontendOrigins =
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

if (allowedFrontendOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "At least one frontend origin must be configured in Cors:AllowedOrigins.");
}

// ---------------------------------------------------------
// MVC / API Controllers
// ---------------------------------------------------------
//
// AddControllersWithViews is used because the AuthController
// uses the MVC [ValidateAntiForgeryToken] filter.
//

builder.Services.AddControllersWithViews();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Frontend",
        policy =>
        {
            policy
                .WithOrigins(allowedFrontendOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

// ---------------------------------------------------------
// OpenAPI
// ---------------------------------------------------------

builder.Services.AddOpenApi();

// ---------------------------------------------------------
// Problem Details
// ---------------------------------------------------------

builder.Services.AddProblemDetails();

// ---------------------------------------------------------
// Infrastructure
// ---------------------------------------------------------
//
// Registers:
// - ApplicationDbContext
// - Azure SQL
// - ASP.NET Core Identity
// - Authentication cookie
// - IAuthenticationService
// - AuthenticationService
// - AuditWriter
// - DatabaseSeeder
//

builder.Services.AddInfrastructure(
    builder.Configuration);

// ---------------------------------------------------------
// Authorization
// ---------------------------------------------------------

builder.Services.AddAuthorization();

// ---------------------------------------------------------
// Anti-forgery / CSRF protection
// ---------------------------------------------------------
//
// Angular will:
//
// 1. GET /api/auth/csrf
// 2. Receive the request token
// 3. Send the token using the X-XSRF-TOKEN header
//
// The antiforgery cookie itself remains HttpOnly.
//

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";

    options.Cookie.Name =
        "__Host-sps-apim-selfserve-xsrf";

    options.Cookie.HttpOnly = true;

    options.Cookie.SecurePolicy =
        CookieSecurePolicy.Always;

    options.Cookie.SameSite =
        SameSiteMode.None;

    options.Cookie.Path = "/";
});

// ---------------------------------------------------------
// Rate limiting
// ---------------------------------------------------------
//
// Authentication endpoints receive stricter rate limiting
// to reduce brute-force login attempts.
//

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddPolicy(
        "authentication",
        httpContext =>
        {
            var partitionKey =
                httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,

                    Window =
                        TimeSpan.FromMinutes(1),

                    QueueLimit = 0,

                    QueueProcessingOrder =
                        QueueProcessingOrder.OldestFirst,

                    AutoReplenishment = true
                });
        });
});

// ---------------------------------------------------------
// Build application
// ---------------------------------------------------------

var app = builder.Build();

// ---------------------------------------------------------
// Bootstrap application data
// ---------------------------------------------------------
//
// Creates:
// - Administrator role
// - User role
// - Bootstrap administrator when enabled
//
// DatabaseSeeder is deliberately idempotent.
//

using (var scope = app.Services.CreateScope())
{
    var databaseSeeder =
        scope.ServiceProvider
            .GetRequiredService<DatabaseSeeder>();

    await databaseSeeder.SeedAsync();
}

// ---------------------------------------------------------
// Exception handling
// ---------------------------------------------------------

app.UseExceptionHandler();

// ---------------------------------------------------------
// HTTPS
// ---------------------------------------------------------

app.UseHttpsRedirection();

// CORS must run before rate limiting and authentication so
// credentialed browser requests and preflight requests are handled.
app.UseCors("Frontend");

// ---------------------------------------------------------
// OpenAPI
// ---------------------------------------------------------
//
// Only expose the OpenAPI document in Development.
//

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// ---------------------------------------------------------
// Rate limiting middleware
// ---------------------------------------------------------

app.UseRateLimiter();

// ---------------------------------------------------------
// Authentication / Authorization
// ---------------------------------------------------------
//
// Authentication must execute before authorization.
//

app.UseAuthentication();

app.UseAuthorization();

// ---------------------------------------------------------
// API Controllers
// ---------------------------------------------------------

app.MapControllers();

// ---------------------------------------------------------
// Root endpoint
// ---------------------------------------------------------

app.MapGet(
        "/",
        () =>
            Results.Ok(
                new
                {
                    application =
                        "SPS APIM Self-Service Portal API",

                    status = "Running"
                }))
    .AllowAnonymous();

// ---------------------------------------------------------
// Liveness health endpoint
// ---------------------------------------------------------

app.MapGet(
        "/health/live",
        () =>
            Results.Ok(
                new
                {
                    status = "Healthy"
                }))
    .AllowAnonymous();

// ---------------------------------------------------------
// Run application
// ---------------------------------------------------------

app.Run();

// ---------------------------------------------------------
// Integration testing support
// ---------------------------------------------------------
//
// Allows WebApplicationFactory<Program> to access the
// generated Program type from the integration test project.
//

public partial class Program
{
}