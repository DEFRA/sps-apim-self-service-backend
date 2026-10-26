namespace Sps.Apim.SelfServe.Application.Notifications;

public interface INotificationService
{
    Task SendPasswordResetEmailAsync(
        string emailAddress,
        string resetUrl,
        CancellationToken cancellationToken);

    Task SendActivationEmailAsync(
        string emailAddress,
        string activationUrl,
        CancellationToken cancellationToken);
}