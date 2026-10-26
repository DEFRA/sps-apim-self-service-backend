using Microsoft.Extensions.Options;
using Notify.Client;
using Sps.Apim.SelfServe.Application.Notifications;
namespace Sps.Apim.SelfServe.Infrastructure.Notifications;

public sealed class GovNotifyService
    : INotificationService
{
    private readonly NotificationClient
        _client;

    private readonly GovNotifyOptions
        _options;

    public GovNotifyService(
        IOptions<GovNotifyOptions> options)
    {
        _options =
            options.Value;

        _client =
            new NotificationClient(
                _options.ApiKey);
    }

    public async Task
        SendPasswordResetEmailAsync(
            string emailAddress,
            string resetUrl,
            CancellationToken cancellationToken)
    {
        await _client.SendEmailAsync(
            emailAddress,
            _options.PasswordResetTemplateId,
            new Dictionary<string, dynamic>
            {
                ["reset_url"] =
                    resetUrl
            });
    }

    public async Task
        SendActivationEmailAsync(
            string emailAddress,
            string activationUrl,
            CancellationToken cancellationToken)
    {
        await _client.SendEmailAsync(
            emailAddress,
            _options.ActivationTemplateId,
            new Dictionary<string, dynamic>
            {
                ["activation_url"] =
                    activationUrl
            });
    }
}