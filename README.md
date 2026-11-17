# APIM Self-Service Portal backend

Backend baseline using .NET 10, ASP.NET Core Identity, EF Core Code First and Azure SQL.

## Before running
Configure `ConnectionStrings:DefaultConnection` outside source control. For Azure App Service, add an application setting named `ConnectionStrings__DefaultConnection` with the Azure SQL connection string; do not commit credentials or environment-specific connection strings.

## Visual Studio migration
Set `Sps.Apim.SelfServe.Api` as Startup Project and run in Package Manager Console:

    Add-Migration InitialIdentityAndPortalSchema -Project Sps.Apim.SelfServe.Infrastructure -StartupProject Sps.Apim.SelfServe.Api -OutputDir Persistence/Migrations
    Update-Database -Project Sps.Apim.SelfServe.Infrastructure -StartupProject Sps.Apim.SelfServe.Api

Review migrations before applying them. Use a controlled deployment process for production database changes.

The user access screen records the administrator who made each access change. Apply the `AddAuditActor` migration before deploying this version.

Access requests are stored in the `AccessRequests` table. Apply the `AddAccessRequests` migration before enabling request submissions or the ICC access request screen.
When an ICC administrator approves a request, the backend creates a pending-activation account, assigns the selected role and optional active project, and sends the existing account activation email. The reviewer must also create an approved GOV.UK Notify decision email template and configure `GovNotify:AccessDecisionTemplateId` (or `GovNotify__AccessDecisionTemplateId` in hosted environments). Its personalisation fields are `first_name`, `decision`, `requested_role`, `project_access`, `decision_note`, and `next_steps`. The decision is recorded separately from email delivery; if Notify fails, the reviewer can retry the decision email from the request detail page.

## User access administration
The seeded account roles include API Developer, API Maintainer, ICC Administrator and Read only. The existing Administrator role remains supported so current administrator accounts retain access. Project assignments are optional and are selected from active projects in the Projects table.

## Endpoints
- GET /api/auth/csrf
- POST /api/auth/sign-in
- POST /api/auth/sign-out
- GET /api/auth/session
- POST /api/auth/change-password
- POST /api/account/access-request
- GET /api/admin/access-requests
- GET /api/admin/access-requests/{requestId}
- POST /api/admin/access-requests/{requestId}/decision
- POST /api/admin/access-requests/{requestId}/decision-email
- GET /api/admin/dashboard-summary
- GET /health/live

## CSRF
Call `/api/auth/csrf`, then send the returned token in `X-XSRF-TOKEN` on state-changing requests. Include browser credentials. The authentication cookie remains HttpOnly.

## Frontend CORS
The checked-in `appsettings.json` intentionally has no allowed origins. Set `Cors__AllowedOrigins__0` as an Azure App Service application setting for each environment, using that environment's exact frontend origin (scheme and host, no path or trailing slash). It can be set in the App Service configuration or by the private Azure DevOps deployment pipeline; do not use `*` because the API uses credentialed browser requests. For local development, add `http://localhost:4200` to `Cors:AllowedOrigins` in your local `appsettings.Development.json`. Startup fails if no frontend origin is configured. Authentication and antiforgery cookies use `SameSite=None` and `Secure` so browsers can send them between the separately hosted frontend and API; keep antiforgery validation enabled for state-changing requests.

## Configuration
`Authentication:CookieName` can be set through runtime configuration (for example, the App Service setting `Authentication__CookieName`); otherwise the API uses its built-in cookie name. The supplied password length, lockout, session, security-stamp validation and rate-limit values are configurable defaults, not an approved production security policy.

Configure GOV.UK Notify credentials outside source control. For local development, store the API key with .NET user secrets:

    dotnet user-secrets set "GovNotify:ApiKey" "<rotated GOV.UK Notify API key>" --project src/Sps.Apim.SelfServe.Api

For hosted environments, set `GovNotify__ApiKey` as a secure application setting or provide it through the approved secrets provider. Rotate any Notify API key that has previously been committed to source control.

### Access request email template
The public access request form sends an email to `giointegrationcapabilitycentre@defra.gov.uk` through GOV.UK Notify. Create an email template in Notify with these personalisation fields:

- `first_name`
- `last_name`
- `email_address`
- `request_type`
- `reason`

Suggested template:

**Subject:** `Portal access request: ((first_name)) ((last_name))`

**Body:**

    An access request has been submitted for the API Management Self-Service Portal.

    Name: ((first_name)) ((last_name))
    Email address: ((email_address))
    Access requested for: ((request_type))

    Reason for access:
    ((reason))

    Review the request and verify the applicant's access requirements before creating an account.

After creating and approving the template, set `GovNotify:AccessRequestTemplateId` to its template ID using .NET user secrets locally or the `GovNotify__AccessRequestTemplateId` secure application setting in hosted environments. The request endpoint returns an error until a template ID is configured; requests are not silently discarded.

### Access decision email template
Create and approve a second GOV.UK Notify email template. Use these personalisation fields exactly:

- `first_name`
- `decision`
- `requested_role`
- `project_access`
- `decision_note`
- `next_steps`

Suggested subject: `Update on your API Management Self-Service Portal access request`

Suggested body:

    Dear ((first_name)),

    We have reviewed your request for access to the API Management Self-Service Portal.

    Outcome: ((decision))
    Portal role: ((requested_role))
    Project access: ((project_access))

    Decision details:
    ((decision_note))

    ((next_steps))

    If you have questions, contact the ICC team.

Set its template ID in local user secrets using `dotnet user-secrets set "GovNotify:AccessDecisionTemplateId" "<template-id>" --project src/Sps.Apim.SelfServe.Api`, or set `GovNotify__AccessDecisionTemplateId` as a secure hosted application setting.

## Account creation
There is intentionally no public registration endpoint. Phase 1 accounts are ICC-managed. Create the initial user through a controlled admin/seeding process without committing passwords to source control.
