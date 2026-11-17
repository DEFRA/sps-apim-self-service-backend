using System.Security.Claims;

namespace Sps.Apim.SelfServe.Application.AccountManagement;

public interface IAccessRequestService
{
    Task SubmitAsync(
        AccessRequestRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AccessRequestResponse>> GetAllAsync(
        CancellationToken cancellationToken);

    Task<AccessRequestResponse?> GetAsync(
        Guid requestId,
        CancellationToken cancellationToken);

    Task<int> GetCountAsync(
        CancellationToken cancellationToken);

    Task<AccessRequestResponse> RecordDecisionAsync(
        ClaimsPrincipal administrator,
        Guid requestId,
        AccessRequestDecisionRequest decision,
        string? correlationId,
        CancellationToken cancellationToken);

    Task SendDecisionEmailAsync(
        Guid requestId,
        CancellationToken cancellationToken);
}
