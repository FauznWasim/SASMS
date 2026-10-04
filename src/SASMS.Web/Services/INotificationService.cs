using SASMS.Web.Models.Entities;

namespace SASMS.Web.Services;

public interface INotificationService
{
    Task NotifyAsync(int userId, string title, string message);
    Task<List<Notification>> GetForUserAsync(int userId);
    Task<int> GetUnreadCountAsync(int userId);
    Task<bool> MarkAsReadAsync(int notificationId, int userId);
}
