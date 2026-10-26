namespace Sps.Apim.SelfServe.Application.AccountManagement;

public interface IAccountRecoveryService
{
    Task<bool> ActivateAsync(
        ActivateAccountRequest request,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<RequestPasswordResetResponse>
        RequestPasswordResetAsync(
            RequestPasswordResetRequest request,
            string? correlationId,
            CancellationToken cancellationToken);

    Task<bool> ResetPasswordAsync(
        CompletePasswordResetRequest request,
        string? correlationId,
        CancellationToken cancellationToken);
}