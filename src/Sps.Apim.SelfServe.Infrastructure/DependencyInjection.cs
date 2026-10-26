using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Sps.Apim.SelfServe.Application.AccountManagement;
using Sps.Apim.SelfServe.Application.Authentication;
using Sps.Apim.SelfServe.Application.Notifications;
using Sps.Apim.SelfServe.Infrastructure.AccountManagement;
using Sps.Apim.SelfServe.Infrastructure.Auditing;
using Sps.Apim.SelfServe.Infrastructure.Authentication;
using Sps.Apim.SelfServe.Infrastructure.Identity;
using Sps.Apim.SelfServe.Infrastructure.Notifications;
using Sps.Apim.SelfServe.Infrastructure.Persistence;

namespace Sps.Apim.SelfServe.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ---------------------------------------------------------
        // Configuration
        // ---------------------------------------------------------

        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found.");

        // ---------------------------------------------------------
        // Entity Framework Core / Azure SQL
        // ---------------------------------------------------------

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(
                connectionString,
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure();
                });
        });

        // ---------------------------------------------------------
        // ASP.NET Core Identity
        // ---------------------------------------------------------

        services
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                // -------------------------------------------------
                // Password policy
                // -------------------------------------------------

                options.Password.RequiredLength = 12;

                options.Password.RequireDigit = true;

                options.Password.RequireLowercase = true;

                options.Password.RequireUppercase = true;

                options.Password.RequireNonAlphanumeric = true;

                // -------------------------------------------------
                // Account lockout
                // -------------------------------------------------

                options.Lockout.AllowedForNewUsers = true;

                options.Lockout.MaxFailedAccessAttempts = 5;

                options.Lockout.DefaultLockoutTimeSpan =
                    TimeSpan.FromMinutes(15);

                // -------------------------------------------------
                // User configuration
                // -------------------------------------------------

                options.User.RequireUniqueEmail = true;

                // Phase 1 users are managed by ICC.
                // Email verification is not currently used as
                // the authentication approval mechanism.
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<SecurityStampValidatorOptions>(
            options =>
            {
                // POC test value only.
                // Production value requires Security approval.
                options.ValidationInterval =
                    TimeSpan.FromMinutes(1);
            });

        services.Configure<GovNotifyOptions>(
            configuration.GetSection(
                GovNotifyOptions.SectionName));

        // ---------------------------------------------------------
        // Authentication cookie
        // ---------------------------------------------------------

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name =
                "__Host-sps-apim-selfserve-auth";

            options.Cookie.HttpOnly = true;

            options.Cookie.SecurePolicy =
                CookieSecurePolicy.Always;

            options.Cookie.SameSite =
                SameSiteMode.Strict;

            options.Cookie.Path = "/";

            options.SlidingExpiration = true;

            options.ExpireTimeSpan =
                TimeSpan.FromMinutes(30);

            // API clients must receive HTTP status codes rather than
            // redirects to HTML login/access-denied pages.

            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode =
                    StatusCodes.Status401Unauthorized;

                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode =
                    StatusCodes.Status403Forbidden;

                return Task.CompletedTask;
            };
        });

        // ---------------------------------------------------------
        // Bootstrap administrator options
        // ---------------------------------------------------------

        services.Configure<BootstrapAdminOptions>(
            configuration.GetSection(
                BootstrapAdminOptions.SectionName));

        // ---------------------------------------------------------
        // Account lifecycle options
        // ---------------------------------------------------------

        services.Configure<AccountLifecycleOptions>(
            configuration.GetSection(
                AccountLifecycleOptions.SectionName));

        // ---------------------------------------------------------
        // Application / infrastructure services
        // ---------------------------------------------------------

        services.AddScoped<DatabaseSeeder>();

        services.AddScoped<AuditWriter>();

        // Authentication

        services.AddScoped<
            IAuthenticationService,
            AuthenticationService>();

        // Account administration

        services.AddScoped<
            IAccountAdministrationService,
            AccountAdministrationService>();

        // Account activation / recovery

        services.AddScoped<
            IAccountRecoveryService,
            AccountRecoveryService>();

        services.AddScoped<
            INotificationService,
            GovNotifyService>();

        return services;
    }
}