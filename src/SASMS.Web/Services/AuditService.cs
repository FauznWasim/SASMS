using SASMS.Web.Data;
using SASMS.Web.Models.Entities;

namespace SASMS.Web.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;

    public AuditService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(int? userId, string action, string entityName, string? entityId = null, string? details = null, string? ipAddress = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            IpAddress = ipAddress,
            TimestampUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}
