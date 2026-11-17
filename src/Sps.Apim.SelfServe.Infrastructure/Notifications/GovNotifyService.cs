using Microsoft.Extensions.Options;
using Notify.Client;
using Sps.Apim.SelfServe.Application.AccountManagement;
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

    public async Task SendAccessRequestEmailAsync(
            AccessRequestRequest request,
            CancellationToken cancellationToken)
    {
            if (string.IsNullOrWhiteSpace(_options.AccessRequestTemplateId))
            {
                throw new InvalidOperationException(
                    "GovNotify:AccessRequestTemplateId must be configured before access requests can be sent.");
            }

            if (string.IsNullOrWhiteSpace(_options.AccessRequestRecipientEmail))
            {
                throw new InvalidOperationException(
                    "GovNotify:AccessRequestRecipientEmail must be configured before access requests can be sent.");
            }

            await _client.SendEmailAsync(
                _options.AccessRequestRecipientEmail,
                _options.AccessRequestTemplateId,
                new Dictionary<string, dynamic>
                {
                    ["first_name"] = request.FirstName,
                    ["last_name"] = request.LastName,
                    ["email_address"] = request.Email,
                    ["request_type"] = request.RequestType,
                    ["reason"] = request.Reason
                });
    }

    public async Task SendAccessDecisionEmailAsync(
        AccessRequestResponse request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AccessDecisionTemplateId))
        {
            throw new InvalidOperationException(
                "GovNotify:AccessDecisionTemplateId must be configured before access decision emails can be sent.");
        }

        var outcome = request.Status switch
        {
            "Approved" => "Approved",
            "Rejected" => "Rejected",
            "MoreInformationRequested" => "More information requested",
            _ => throw new InvalidOperationException(
                "A decision email can only be sent for a completed decision.")
        };
        var nextSteps = request.Status == "Approved"
            ? "An activation email with instructions has been sent separately."
            : "Contact the ICC team if you have questions about this decision.";

        await _client.SendEmailAsync(
            request.Email,
            _options.AccessDecisionTemplateId,
            new Dictionary<string, dynamic>
            {
                ["first_name"] = request.FirstName,
                ["decision"] = outcome,
                ["requested_role"] = request.AssignedRole ?? "Not applicable",
                ["project_access"] = request.ProjectName ?? "No project assigned",
                ["decision_note"] = request.DecisionNote ?? string.Empty,
                ["next_steps"] = nextSteps
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