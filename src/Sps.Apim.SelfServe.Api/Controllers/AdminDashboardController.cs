using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sps.Apim.SelfServe.Application.AccountManagement;
using Sps.Apim.SelfServe.Domain.Constants;

namespace Sps.Apim.SelfServe.Api.Controllers;

[ApiController]
[Route("api/admin/dashboard-summary")]
[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.IccAdministrator)]
public sealed class AdminDashboardController(
    IAccountAdministrationService accounts,
    IAccessRequestService accessRequests)
    : ControllerBase
{
    [HttpGet]
    public async Task<AdminDashboardSummaryResponse> GetSummary(
        CancellationToken cancellationToken)
    {
        var portalUserCount =
            await accounts.GetUserCountAsync(cancellationToken);
        var accessRequestCount =
            await accessRequests.GetCountAsync(cancellationToken);

        return new AdminDashboardSummaryResponse
        {
            PortalUserCount = portalUserCount,
            AccessRequestsAwaitingReviewCount = accessRequestCount,
            ActiveOnboardingRequestsCount = null
        };
    }
}
