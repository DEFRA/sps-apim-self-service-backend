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

    Task<int> GetUserCountAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ProjectResponse>> GetProjectsAsync(
        CancellationToken cancellationToken);

    Task<UserAccessResponse> GetUserAccessAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task UpdateUserAccessAsync(
        ClaimsPrincipal administrator,
        Guid userId,
        UpdateUserAccessRequest request,
        string? correlationId,
        CancellationToken cancellationToken);

    Task UnlockAsync(
        ClaimsPrincipal administrator,
        Guid userId,
        string? correlationId,
        CancellationToken cancellationToken);

    Task DisableAsync(
        ClaimsPrincipal administrator,
        Guid userId,
        string reason,
        string? correlationId,
        CancellationToken cancellationToken);

    Task EnableAsync(
        ClaimsPrincipal administrator,
        Guid userId,
        string reason,
        string? correlationId,
        CancellationToken cancellationToken);
}