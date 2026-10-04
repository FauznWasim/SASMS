using Microsoft.EntityFrameworkCore;
using SASMS.Web.Data;
using SASMS.Web.Models.Enums;
using SASMS.Web.Services.Dtos;

namespace SASMS.Web.Services;

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _db;

    public ReportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<MonthlyAttendanceRow>> GetMonthlyAsync(int year, int month)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var records = await _db.Attendances
            .Include(a => a.Employee)
            .Where(a => a.AttendanceDate >= from && a.AttendanceDate <= to)
            .ToListAsync();

        return records
            .GroupBy(a => new { a.EmployeeId, a.Employee.FullName })
            .Select(g => new MonthlyAttendanceRow(
                g.Key.EmployeeId,
                g.Key.FullName,
                DaysPresent: g.Count(a => a.AttendanceStatus == AttendanceStatus.Present && a.VerificationStatus == VerificationStatus.Verified),
                DaysLate: g.Count(a => a.AttendanceStatus == AttendanceStatus.Late && a.VerificationStatus == VerificationStatus.Verified),
                DaysAbsent: g.Count(a => a.AttendanceStatus == AttendanceStatus.Absent),
                DaysOnLeave: g.Count(a => a.AttendanceStatus == AttendanceStatus.OnLeave),
                TotalHoursWorked: g.Where(a => a.VerificationStatus == VerificationStatus.Verified && a.CheckInTime != null && a.CheckOutTime != null)
                    .Sum(a => (a.CheckOutTime!.Value - a.CheckInTime!.Value).TotalHours)))
            .OrderBy(r => r.EmployeeName)
            .ToList();
    }

    public async Task<List<LateAttendanceRow>> GetLateAsync(DateOnly from, DateOnly to)
    {
        var records = await _db.Attendances
            .Include(a => a.Employee)
            .Where(a => a.AttendanceDate >= from && a.AttendanceDate <= to && a.AttendanceStatus == AttendanceStatus.Late)
            .ToListAsync();

        var schedules = await _db.Schedules
            .Where(s => s.ShiftDate >= from && s.ShiftDate <= to)
            .ToListAsync();

        return records
            .Select(a =>
            {
                var schedule = schedules.FirstOrDefault(s => s.EmployeeId == a.EmployeeId && s.ShiftDate == a.AttendanceDate);
                var minutesLate = 0;
                if (schedule is not null && a.CheckInTime is not null)
                {
                    var scheduledStart = a.AttendanceDate.ToDateTime(schedule.StartTime, DateTimeKind.Utc);
                    minutesLate = Math.Max(0, (int)(a.CheckInTime.Value - scheduledStart).TotalMinutes);
                }

                return new LateAttendanceRow(a.AttendanceId, a.Employee.FullName, a.AttendanceDate, a.CheckInTime, minutesLate);
            })
            .OrderByDescending(r => r.AttendanceDate)
            .ToList();
    }

    public async Task<List<AbsenceRow>> GetAbsenceAsync(DateOnly from, DateOnly to)
    {
        return await _db.Attendances
            .Include(a => a.Employee)
            .Where(a => a.AttendanceDate >= from && a.AttendanceDate <= to && a.AttendanceStatus == AttendanceStatus.Absent)
            .OrderByDescending(a => a.AttendanceDate)
            .Select(a => new AbsenceRow(a.Employee.FullName, a.AttendanceDate))
            .ToListAsync();
    }

    public async Task<List<OvertimeRow>> GetOvertimeAsync(DateOnly from, DateOnly to, double standardHoursPerDay = 8)
    {
        var workHours = await GetWorkHoursAsync(from, to, standardHoursPerDay);

        return workHours
            .Where(r => r.OvertimeHours > 0)
            .Select(r => new OvertimeRow(r.EmployeeName, r.AttendanceDate, r.HoursWorked, r.OvertimeHours))
            .OrderByDescending(r => r.AttendanceDate)
            .ToList();
    }

    public async Task<List<WorkHoursRow>> GetWorkHoursAsync(DateOnly from, DateOnly to, double standardHoursPerDay = 8)
    {
        var records = await _db.Attendances
            .Include(a => a.Employee)
            .Where(a => a.AttendanceDate >= from && a.AttendanceDate <= to
                && a.VerificationStatus == VerificationStatus.Verified
                && a.CheckInTime != null && a.CheckOutTime != null)
            .ToListAsync();

        return records
            .Select(a =>
            {
                var hoursWorked = (a.CheckOutTime!.Value - a.CheckInTime!.Value).TotalHours;
                return new WorkHoursRow(a.EmployeeId, a.Employee.FullName, a.AttendanceDate, hoursWorked, Math.Max(0, hoursWorked - standardHoursPerDay));
            })
            .OrderBy(r => r.AttendanceDate)
            .ToList();
    }
}
