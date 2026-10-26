namespace Sps.Apim.SelfServe.Infrastructure.Notifications;

public sealed class GovNotifyOptions
{
    public const string SectionName =
        "GovNotify";

    public string ApiKey
    {
        get;
        init;
    } = string.Empty;

    public string PasswordResetTemplateId
    {
        get;
        init;
    } = string.Empty;

    public string ActivationTemplateId
    {
        get;
        init;
    } = string.Empty;
}