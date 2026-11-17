namespace Sps.Apim.SelfServe.Application.Notifications;

using Sps.Apim.SelfServe.Application.AccountManagement;

public interface INotificationService
{
    Task SendPasswordResetEmailAsync(
        string emailAddress,
        string resetUrl,
        CancellationToken cancellationToken);

    Task SendAccessRequestEmailAsync(
        AccessRequestRequest request,
        CancellationToken cancellationToken);

    Task SendAccessDecisionEmailAsync(
        AccessRequestResponse request,
        CancellationToken cancellationToken);

    Task SendActivationEmailAsync(
        string emailAddress,
        string activationUrl,
        CancellationToken cancellationToken);
}