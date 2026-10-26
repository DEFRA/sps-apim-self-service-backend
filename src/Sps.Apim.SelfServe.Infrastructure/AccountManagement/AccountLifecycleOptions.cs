namespace Sps.Apim.SelfServe.Infrastructure.AccountManagement;

public sealed class AccountLifecycleOptions
{
    public const string SectionName =
        "AccountLifecycle";

    public int ActivationTokenLifetimeMinutes
    {
        get;
        init;
    } = 60;

    public int PasswordResetTokenLifetimeMinutes
    {
        get;
        init;
    } = 30;

    public string PortalBaseUrl
    {
        get;
        init;
    } = string.Empty;
}