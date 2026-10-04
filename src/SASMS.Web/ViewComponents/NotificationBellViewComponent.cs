using Microsoft.AspNetCore.Mvc;
using SASMS.Web.Security;
using SASMS.Web.Services;

namespace SASMS.Web.ViewComponents;

public class NotificationBellViewComponent : ViewComponent
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserService _currentUser;

    public NotificationBellViewComponent(INotificationService notificationService, ICurrentUserService currentUser)
    {
        _notificationService = notificationService;
        _currentUser = currentUser;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var unreadCount = _currentUser.IsAuthenticated
            ? await _notificationService.GetUnreadCountAsync(_currentUser.UserId)
            : 0;

        return View(unreadCount);
    }
}
