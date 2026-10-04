using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SASMS.Web.Models.ViewModels;
using SASMS.Web.Security;
using SASMS.Web.Services;
using SASMS.Web.Services.Dtos;

namespace SASMS.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ICurrentUserService _currentUser;
    private readonly IAttendanceService _attendanceService;
    private readonly IScheduleService _scheduleService;
    private readonly INotificationService _notificationService;
    private readonly IEmployeeService _employeeService;
    private readonly ILeaveService _leaveService;
    private readonly IReportService _reportService;
    private readonly IBusinessClock _businessClock;

    public HomeController(
        ICurrentUserService currentUser,
        IAttendanceService attendanceService,
        IScheduleService scheduleService,
        INotificationService notificationService,
        IEmployeeService employeeService,
        ILeaveService leaveService,
        IReportService reportService,
        IBusinessClock businessClock)
    {
        _currentUser = currentUser;
        _attendanceService = attendanceService;
        _scheduleService = scheduleService;
        _notificationService = notificationService;
        _employeeService = employeeService;
        _leaveService = leaveService;
        _reportService = reportService;
        _businessClock = businessClock;
    }

    public IActionResult Index()
    {
        return _currentUser.IsManager
            ? RedirectToAction(nameof(ManagerDashboard))
            : RedirectToAction(nameof(EmployeeDashboard));
    }

    [Authorize(Roles = RoleNames.Employee)]
    public async Task<IActionResult> EmployeeDashboard()
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId is null)
        {
            return RedirectToAction(nameof(AccountController.AccessDenied), "Account");
        }

        var today = _businessClock.Today;
        var history = await _attendanceService.GetHistoryAsync(employeeId.Value, today.AddDays(-7), today);
        var upcomingSchedules = await _scheduleService.GetByEmployeeAsync(employeeId.Value, today, today.AddDays(14));
        var unreadCount = await _notificationService.GetUnreadCountAsync(_currentUser.UserId);

        ViewBag.RecentAttendance = history;
        ViewBag.UpcomingSchedules = upcomingSchedules;
        ViewBag.UnreadNotificationCount = unreadCount;
        ViewBag.AttendanceStatus = await BuildAttendanceStatusCardAsync(employeeId.Value, today);
        ViewBag.DisplayName = await GetDisplayNameAsync();

        return View();
    }

    [Authorize(Roles = RoleNames.Manager)]
    public async Task<IActionResult> ManagerDashboard(string period = "today")
    {
        var today = _businessClock.Today;
        ViewBag.TodayAttendance = await _attendanceService.GetAllAsync(today, today);
        ViewBag.UpcomingSchedules = await _scheduleService.GetAllAsync(today, today.AddDays(7));
        ViewBag.PendingLeaveCount = await _leaveService.GetPendingCountAsync();
        ViewBag.WorkHoursOverview = await BuildWorkHoursOverviewAsync(period, today);
        ViewBag.DisplayName = await GetDisplayNameAsync();

        // Manager/PIC is also store staff and checks in/out for themselves — see the doc's
        // "Perform their own attendance check-in/check-out where applicable" (README §6.2).
        var employeeId = _currentUser.EmployeeId;
        ViewBag.AttendanceStatus = employeeId is not null
            ? await BuildAttendanceStatusCardAsync(employeeId.Value, today)
            : new AttendanceStatusCardViewModel();

        return View();
    }

    /// <summary>Returns just the Work Hours Overview markup (no layout) for the dashboard's
    /// period buttons to swap in via fetch() — avoids a full-page reload, so the user's
    /// scroll position never moves. Reuses the exact same BuildWorkHoursOverviewAsync/
    /// GetWorkHoursAsync calculation as ManagerDashboard's initial render.</summary>
    [Authorize(Roles = RoleNames.Manager)]
    public async Task<IActionResult> WorkHoursOverviewPartial(string period = "today")
    {
        var model = await BuildWorkHoursOverviewAsync(period, _businessClock.Today);
        return PartialView("_WorkHoursOverview", model);
    }

    /// <summary>Built entirely from IReportService.GetWorkHoursAsync — the same Verified/
    /// both-times-present worked-hours calculation the Overtime/Monthly reports use, so the
    /// dashboard's numbers can never disagree with the Reports page.</summary>
    private async Task<WorkHoursOverviewViewModel> BuildWorkHoursOverviewAsync(string period, DateOnly today)
    {
        DateOnly from, to;
        switch (period)
        {
            case "week":
                var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7; // Sunday=0 -> Monday-based week
                from = today.AddDays(-daysSinceMonday);
                to = from.AddDays(6);
                break;
            case "month":
                from = new DateOnly(today.Year, today.Month, 1);
                to = from.AddMonths(1).AddDays(-1);
                break;
            default:
                period = "today";
                from = today;
                to = today;
                break;
        }

        var rows = await _reportService.GetWorkHoursAsync(from, to);

        var chartPoints = period switch
        {
            "week" => BuildDailyChartPoints(rows, from, to),
            "month" => BuildWeeklyChartPoints(rows, from, to),
            _ => rows
                .GroupBy(r => r.EmployeeName)
                .Select(g => new WorkHoursChartPoint(g.Key, Math.Round(g.Sum(r => r.HoursWorked), 1), Math.Round(g.Sum(r => r.OvertimeHours), 1)))
                .OrderByDescending(p => p.TotalHours)
                .ToList()
        };

        return new WorkHoursOverviewViewModel
        {
            Period = period,
            TotalWorkedHours = Math.Round(rows.Sum(r => r.HoursWorked), 1),
            TotalOvertimeHours = Math.Round(rows.Sum(r => r.OvertimeHours), 1),
            EmployeesTracked = rows.Select(r => r.EmployeeId).Distinct().Count(),
            ChartPoints = chartPoints
        };
    }

    private static List<WorkHoursChartPoint> BuildDailyChartPoints(List<WorkHoursRow> rows, DateOnly from, DateOnly to)
    {
        var points = new List<WorkHoursChartPoint>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var dayRows = rows.Where(r => r.AttendanceDate == d).ToList();
            points.Add(new WorkHoursChartPoint(d.ToString("ddd"), Math.Round(dayRows.Sum(r => r.HoursWorked), 1), Math.Round(dayRows.Sum(r => r.OvertimeHours), 1)));
        }
        return points;
    }

    private static List<WorkHoursChartPoint> BuildWeeklyChartPoints(List<WorkHoursRow> rows, DateOnly from, DateOnly to)
    {
        var points = new List<WorkHoursChartPoint>();
        var weekStart = from;
        var weekNumber = 1;
        while (weekStart <= to)
        {
            var weekEnd = weekStart.AddDays(6) > to ? to : weekStart.AddDays(6);
            var weekRows = rows.Where(r => r.AttendanceDate >= weekStart && r.AttendanceDate <= weekEnd).ToList();
            points.Add(new WorkHoursChartPoint($"Week {weekNumber}", Math.Round(weekRows.Sum(r => r.HoursWorked), 1), Math.Round(weekRows.Sum(r => r.OvertimeHours), 1)));
            weekStart = weekStart.AddDays(7);
            weekNumber++;
        }
        return points;
    }

    /// <summary>The employee's real name when the account is linked to one, falling back to
    /// the login username for accounts with no Employee record (e.g. a pure admin login).</summary>
    private async Task<string> GetDisplayNameAsync()
    {
        if (_currentUser.EmployeeId is int employeeId)
        {
            var employee = await _employeeService.GetByIdAsync(employeeId);
            if (employee is not null)
            {
                return employee.FullName;
            }
        }
        return _currentUser.Username;
    }

    private async Task<AttendanceStatusCardViewModel> BuildAttendanceStatusCardAsync(int employeeId, DateOnly today)
    {
        var history = await _attendanceService.GetHistoryAsync(employeeId, today, today);
        var employee = await _employeeService.GetByIdAsync(employeeId);

        return new AttendanceStatusCardViewModel
        {
            TodayRecord = history.FirstOrDefault(a => a.AttendanceDate == today),
            FaceRegistered = employee?.FaceTemplateRef is not null,
            HasScheduleToday = await _attendanceService.HasScheduledShiftAsync(employeeId, today)
        };
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
