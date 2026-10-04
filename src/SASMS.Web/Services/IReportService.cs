using SASMS.Web.Services.Dtos;

namespace SASMS.Web.Services;

public interface IReportService
{
    Task<List<MonthlyAttendanceRow>> GetMonthlyAsync(int year, int month);
    Task<List<LateAttendanceRow>> GetLateAsync(DateOnly from, DateOnly to);
    Task<List<AbsenceRow>> GetAbsenceAsync(DateOnly from, DateOnly to);
    Task<List<OvertimeRow>> GetOvertimeAsync(DateOnly from, DateOnly to, double standardHoursPerDay = 8);

    /// <summary>The same Verified/both-times-present worked-hours calculation GetOvertimeAsync
    /// is built on, but returns every record (not just ones with overtime) — the shared
    /// source-of-truth for the Manager Dashboard's Work Hours Overview.</summary>
    Task<List<WorkHoursRow>> GetWorkHoursAsync(DateOnly from, DateOnly to, double standardHoursPerDay = 8);
}
