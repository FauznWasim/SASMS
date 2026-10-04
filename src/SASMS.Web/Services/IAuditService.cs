namespace SASMS.Web.Services;

public interface IAuditService
{
    Task LogAsync(int? userId, string action, string entityName, string? entityId = null, string? details = null, string? ipAddress = null);
}
