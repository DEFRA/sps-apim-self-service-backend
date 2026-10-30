# APIM Self-Service Portal backend

Backend baseline using .NET 10, ASP.NET Core Identity, EF Core Code First and Azure SQL.

## Before running
Update `ConnectionStrings:DefaultConnection`. The checked-in placeholder uses `Authentication=Active Directory Default` and contains no password.

## Visual Studio migration
Set `Sps.Apim.SelfServe.Api` as Startup Project and run in Package Manager Console:

    Add-Migration InitialIdentityAndPortalSchema -Project Sps.Apim.SelfServe.Infrastructure -StartupProject Sps.Apim.SelfServe.Api -OutputDir Persistence/Migrations
    Update-Database -Project Sps.Apim.SelfServe.Infrastructure -StartupProject Sps.Apim.SelfServe.Api

Review migrations before applying them. Use a controlled deployment process for production database changes.

## Endpoints
- GET /api/auth/csrf
- POST /api/auth/sign-in
- POST /api/auth/sign-out
- GET /api/auth/session
- POST /api/auth/change-password
- GET /health/live

## CSRF
Call `/api/auth/csrf`, then send the returned token in `X-XSRF-TOKEN` on state-changing requests. Include browser credentials. The authentication cookie remains HttpOnly.

## Frontend CORS
Configure `Cors:AllowedOrigins` with the exact origin(s) of trusted frontend deployments, including scheme and host but no path. For example, set the API App Service application setting `Cors__AllowedOrigins__0` to `https://<frontend-app>.azurewebsites.net`. Do not use `*`: the API uses credentialed browser requests. For local development, add `http://localhost:4200` to `Cors:AllowedOrigins` in your local `appsettings.Development.json`; production startup fails if no frontend origin is configured. Authentication and antiforgery cookies use `SameSite=None` and `Secure` so browsers can send them between the separately hosted frontend and API; keep antiforgery validation enabled for state-changing requests.

## Configuration
The supplied password length, lockout, session, security-stamp validation and rate-limit values are configurable defaults, not an approved production security policy.

## Account creation
There is intentionally no public registration endpoint. Phase 1 accounts are ICC-managed. Create the initial user through a controlled admin/seeding process without committing passwords to source control.
