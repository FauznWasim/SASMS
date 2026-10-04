namespace SASMS.Web.Security;

/// <summary>Role claim values used by [Authorize(Roles = ...)]. Must match Role.RoleName seed values.</summary>
public static class RoleNames
{
    public const string Employee = "Employee";
    public const string Manager = "Manager/PIC";
}
