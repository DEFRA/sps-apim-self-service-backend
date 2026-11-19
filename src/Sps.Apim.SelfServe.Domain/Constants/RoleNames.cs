namespace Sps.Apim.SelfServe.Domain.Constants;

public static class RoleNames
{
    public const string Administrator = "Administrator";
    public const string User = "User";
    public const string ApiDeveloper = "API Developer";
    public const string ApiMaintainer = "API Maintainer";
    public const string IccAdministrator = "ICC Administrator";
    public const string ReadOnly = "Read only";

    public static readonly string[] ManageAccessRoles =
    [
        ApiDeveloper,
        ApiMaintainer,
        IccAdministrator,
        Administrator,
        ReadOnly
    ];

}