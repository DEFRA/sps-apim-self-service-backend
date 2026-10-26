using System.Security.Claims;

namespace Sps.Apim.SelfServe.Application.AccountManagement;

public interface IAccountAdministrationService
{
    Task<CreateUserResponse> CreateUserAsync(
        ClaimsPrincipal administrator,
        CreateUserRequest request,
        string? correlationId,
        CancellationToken cancellationToken);
    
    Task<ReissueActivationResponse>
    ReissueActivationAsync(
        ClaimsPrincipal administrator,
        Guid userId,
        ReissueActivationRequest request,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<UserSummaryResponse>>
    GetUsersAsync(
        CancellationToken cancellationToken);

    Task ReissueActivationAsync(
        Guid userId,
        string reason,
        string? correlationId,
        CancellationToken cancellationToken);

    Task UnlockAsync(
        Guid userId,
        string? correlationId,
        CancellationToken cancellationToken);

    Task DisableAsync(
        Guid userId,
        string reason,
        string? correlationId,
        CancellationToken cancellationToken);

    Task EnableAsync(
        Guid userId,
        string reason,
        string? correlationId,
        CancellationToken cancellationToken);
}