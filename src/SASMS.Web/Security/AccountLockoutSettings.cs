namespace SASMS.Web.Security;

public class AccountLockoutSettings
{
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}
