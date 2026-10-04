using System.Security.Claims;

namespace SASMS.Web.Security;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public int UserId
    {
        get
        {
            var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : 0;
        }
    }

    public int? EmployeeId
    {
        get
        {
            var value = Principal?.FindFirstValue(AppClaimTypes.EmployeeId);
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public string Username => Principal?.Identity?.Name ?? string.Empty;

    public bool IsManager => Principal?.IsInRole(RoleNames.Manager) ?? false;

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}

public static class AppClaimTypes
{
    public const string EmployeeId = "sasms:employee_id";

    /// <summary>Unix-seconds timestamp of when the session was authenticated — backs the absolute session timeout.</summary>
    public const string AuthTime = "sasms:auth_time";
}
