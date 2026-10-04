using Microsoft.EntityFrameworkCore;
using SASMS.Web.Data;
using SASMS.Web.Models.Entities;
using SASMS.Web.Models.Enums;
using SASMS.Web.Security;
using SASMS.Web.Services.Dtos;

namespace SASMS.Web.Services;

public class AttendanceService : IAttendanceService
{
    private static readonly TimeSpan LateGrace = TimeSpan.FromMinutes(15);

    /// <summary>Failed live-verification attempts allowed per day, per action (check-in/check-out
    /// tracked separately) — caps retrying against the live camera before requiring a Manager/PIC.</summary>
    public const int MaxAttemptsPerDay = 3;

    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;
    private readonly IBusinessClock _businessClock;

    public AttendanceService(ApplicationDbContext db, IAuditService auditService, INotificationService notificationService, IBusinessClock businessClock)
    {
        _db = db;
        _auditService = auditService;
        _notificationService = notificationService;
        _businessClock = businessClock;
    }

    public async Task<ServiceResult<Attendance>> RecordVerifiedCheckInAsync(int employeeId, int actingUserId, string? ipAddress)
    {
        var today = _businessClock.Today;

        var existing = await _db.Attendances
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttendanceDate == today);
        if (existing is not null)
        {
            return ServiceResult<Attendance>.Fail("You have already checked in today.");
        }

        // Defense in depth: AttendanceController.CheckIn already refuses to even start facial
        // verification without a schedule, but this method must independently refuse to record
        // a Present attendance for an unscheduled day even if reached some other way — the
        // Schedule table is the sole authority for "expected to work today" (no Full-Time/
        // Part-Time flag exists or is consulted anywhere in this system).
        var schedule = await _db.Schedules
            .FirstOrDefaultAsync(s => s.EmployeeId == employeeId && s.ShiftDate == today);
        if (schedule is null)
        {
            return ServiceResult<Attendance>.Fail("You are not scheduled to work today.");
        }

        var now = DateTime.UtcNow;
        // schedule.StartTime is Malaysia wall-clock time (entered by a Manager who means
        // local time, not UTC) — must be converted through the business clock to get the
        // real UTC instant it represents before comparing against `now`. Previously this
        // was constructed as `today.ToDateTime(schedule.StartTime, DateTimeKind.Utc)`, which
        // only tags the value as UTC without converting it — .NET's DateTime comparison
        // operators ignore .Kind entirely, so that silently compared "09:00" against a real
        // UTC instant as if they were the same reference frame, adding a hidden ~8-hour
        // leniency to the Late determination on every single check-in, not just near midnight.
        var scheduledStartUtc = _businessClock.ConvertBusinessLocalToUtc(today, schedule.StartTime);
        var status = now > scheduledStartUtc.Add(LateGrace) ? AttendanceStatus.Late : AttendanceStatus.Present;

        var attendance = new Attendance
        {
            EmployeeId = employeeId,
            AttendanceDate = today,
            CheckInTime = now,
            AttendanceStatus = status,
            VerificationStatus = VerificationStatus.Verified,
            CreatedAtUtc = now
        };

        _db.Attendances.Add(attendance);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (DbConcurrencyHelper.IsDuplicateKeyViolation(ex))
        {
            return ServiceResult<Attendance>.Fail("You have already checked in today.");
        }

        await ResetAttemptCountersAsync(employeeId);

        await _auditService.LogAsync(actingUserId, "CheckIn", "Attendance", attendance.AttendanceId.ToString(),
            $"Employee {employeeId} checked in and was verified.", ipAddress);

        return ServiceResult<Attendance>.Ok(attendance);
    }

    public async Task<ServiceResult> CheckOutAsync(int employeeId, int actingUserId, string? ipAddress)
    {
        var today = _businessClock.Today;

        var attendance = await _db.Attendances
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttendanceDate == today);
        if (attendance is null)
        {
            return ServiceResult.Fail("You have not checked in today.");
        }

        if (attendance.CheckOutTime is not null)
        {
            return ServiceResult.Fail("You have already checked out today.");
        }

        attendance.CheckOutTime = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(actingUserId, "CheckOut", "Attendance", attendance.AttendanceId.ToString(),
            $"Employee {employeeId} checked out.", ipAddress);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ApplyVerificationResultAsync(int employeeId, DateOnly attendanceDate, bool success, bool livenessPassed)
    {
        var attendance = await _db.Attendances
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttendanceDate == attendanceDate);
        if (attendance is null)
        {
            return ServiceResult.Fail("No matching attendance record found for that employee/date.");
        }

        attendance.VerificationStatus = success && livenessPassed ? VerificationStatus.Verified : VerificationStatus.Failed;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(null, "VerificationCallback", "Attendance", attendance.AttendanceId.ToString(),
            $"Facial verification service reported success={success}, liveness={livenessPassed}.", null);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ManualVerificationOverrideAsync(int attendanceId, int actingUserId, string? ipAddress)
    {
        var attendance = await _db.Attendances.FirstOrDefaultAsync(a => a.AttendanceId == attendanceId);
        if (attendance is null)
        {
            return ServiceResult.Fail("Attendance record not found.");
        }

        // NotApplicable means no facial verification was ever meant to happen for this record
        // (Absent, OnLeave) — protects against a direct POST bypassing the UI, not just hiding
        // the button; the record is left completely unmodified.
        if (attendance.VerificationStatus == VerificationStatus.NotApplicable)
        {
            return ServiceResult.Fail("Facial verification does not apply to this record (Absent or On Leave).");
        }

        attendance.VerificationStatus = VerificationStatus.Verified;
        await _db.SaveChangesAsync();

        // Deliberately loud audit entry: this bypasses real facial/liveness verification and must
        // never be mistaken for the secure flow. Kept as the documented fallback for when the real
        // check (built, but tunable) genuinely can't confirm someone — not because it's unbuilt.
        await _auditService.LogAsync(actingUserId, "ManualVerificationOverride", "Attendance", attendanceId.ToString(),
            "TEST-ONLY manual override used in place of a real facial/liveness verification result.",
            ipAddress);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ManualCheckOutOverrideAsync(int attendanceId, int actingUserId, string? ipAddress)
    {
        var attendance = await _db.Attendances.FirstOrDefaultAsync(a => a.AttendanceId == attendanceId);
        if (attendance is null)
        {
            return ServiceResult.Fail("Attendance record not found.");
        }

        // Same protection as ManualVerificationOverrideAsync — Absent/OnLeave records must
        // never receive a check-out time, regardless of how this endpoint is reached.
        if (attendance.VerificationStatus == VerificationStatus.NotApplicable)
        {
            return ServiceResult.Fail("Facial verification does not apply to this record (Absent or On Leave).");
        }

        if (attendance.CheckOutTime is not null)
        {
            return ServiceResult.Fail("This record already has a check-out time.");
        }

        attendance.CheckOutTime = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(actingUserId, "ManualCheckOutOverride", "Attendance", attendanceId.ToString(),
            "TEST-ONLY manual override used to record check-out after the live face scan couldn't confirm it.",
            ipAddress);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ManualCheckInOverrideAsync(int employeeId, int actingUserId, string? ipAddress)
    {
        var today = _businessClock.Today;

        // Same rule as the live check-in path: no Schedule row means no attendance, even
        // for a Manager/PIC override — this bypasses the camera, not the schedule.
        if (!await HasScheduledShiftAsync(employeeId, today))
        {
            return ServiceResult.Fail("This employee is not scheduled to work today and cannot be manually checked in.");
        }

        var existing = await _db.Attendances
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttendanceDate == today);
        if (existing is not null)
        {
            return ServiceResult.Fail("An attendance record already exists for this employee today.");
        }

        var attendance = new Attendance
        {
            EmployeeId = employeeId,
            AttendanceDate = today,
            CheckInTime = DateTime.UtcNow,
            AttendanceStatus = AttendanceStatus.Present,
            VerificationStatus = VerificationStatus.Verified,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Attendances.Add(attendance);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (DbConcurrencyHelper.IsDuplicateKeyViolation(ex))
        {
            return ServiceResult.Fail("An attendance record already exists for this employee today.");
        }

        await ResetAttemptCountersAsync(employeeId);

        await _auditService.LogAsync(actingUserId, "ManualCheckInOverride", "Attendance", attendance.AttendanceId.ToString(),
            "TEST-ONLY manual override used to record a check-in without a live verification pass (e.g. after too many failed attempts).",
            ipAddress);

        return ServiceResult.Ok();
    }

    public async Task<int> RegisterFailedAttemptAsync(int employeeId, bool isCheckIn, int actingUserId, string? ipAddress)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        if (employee is null)
        {
            return 0;
        }

        RollAttemptCountersIfNewDay(employee, _businessClock.Today);

        if (isCheckIn)
        {
            employee.FailedCheckInAttempts++;
        }
        else
        {
            employee.FailedCheckOutAttempts++;
        }

        await _db.SaveChangesAsync();

        var used = isCheckIn ? employee.FailedCheckInAttempts : employee.FailedCheckOutAttempts;
        var remaining = Math.Max(0, MaxAttemptsPerDay - used);

        await _auditService.LogAsync(actingUserId, isCheckIn ? "CheckInVerificationFailed" : "CheckOutVerificationFailed",
            "Employee", employeeId.ToString(),
            $"Failed live verification attempt {used}/{MaxAttemptsPerDay} today.", ipAddress);

        return remaining;
    }

    public async Task<AttemptStatus> GetAttemptStatusAsync(int employeeId)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        if (employee is null)
        {
            return new AttemptStatus(0, 0, false, false);
        }

        var today = _businessClock.Today;
        var checkInUsed = employee.AttemptsResetDate == today ? employee.FailedCheckInAttempts : 0;
        var checkOutUsed = employee.AttemptsResetDate == today ? employee.FailedCheckOutAttempts : 0;

        return new AttemptStatus(checkInUsed, checkOutUsed, checkInUsed >= MaxAttemptsPerDay, checkOutUsed >= MaxAttemptsPerDay);
    }

    public async Task<ServiceResult> ResetVerificationAttemptsAsync(int employeeId, int actingUserId, string? ipAddress)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        if (employee is null)
        {
            return ServiceResult.Fail("Employee not found.");
        }

        employee.FailedCheckInAttempts = 0;
        employee.FailedCheckOutAttempts = 0;
        employee.AttemptsResetDate = _businessClock.Today;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(actingUserId, "ResetVerificationAttempts", "Employee", employeeId.ToString(),
            $"Reset today's failed verification attempt counters for '{employee.FullName}'.", ipAddress);

        return ServiceResult.Ok();
    }

    private async Task ResetAttemptCountersAsync(int employeeId)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        if (employee is null)
        {
            return;
        }

        employee.FailedCheckInAttempts = 0;
        employee.FailedCheckOutAttempts = 0;
        employee.AttemptsResetDate = _businessClock.Today;
        await _db.SaveChangesAsync();
    }

    private static void RollAttemptCountersIfNewDay(Employee employee, DateOnly today)
    {
        if (employee.AttemptsResetDate != today)
        {
            employee.AttemptsResetDate = today;
            employee.FailedCheckInAttempts = 0;
            employee.FailedCheckOutAttempts = 0;
        }
    }

    public async Task<ServiceResult> MarkAbsentAsync(int employeeId, DateOnly date, int actingUserId, string? ipAddress)
    {
        // A rest day (no Schedule row) is not Absent — only an employee who was actually
        // expected to work can be marked absent for not showing up.
        var isScheduled = await HasScheduledShiftAsync(employeeId, date);
        if (!isScheduled)
        {
            return ServiceResult.Fail("This employee is not scheduled to work on the selected date and cannot be marked absent.");
        }

        var existing = await _db.Attendances
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttendanceDate == date);
        if (existing is not null)
        {
            return ServiceResult.Fail("An attendance record already exists for that employee/date.");
        }

        var attendance = new Attendance
        {
            EmployeeId = employeeId,
            AttendanceDate = date,
            AttendanceStatus = AttendanceStatus.Absent,
            // No facial verification was attempted — this is a management decision, not a
            // verified (or failed) biometric event, so it must not read as either.
            VerificationStatus = VerificationStatus.NotApplicable,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Attendances.Add(attendance);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (DbConcurrencyHelper.IsDuplicateKeyViolation(ex))
        {
            return ServiceResult.Fail("An attendance record already exists for that employee/date.");
        }

        var employee = await _db.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        if (employee?.User is not null)
        {
            await _notificationService.NotifyAsync(employee.User.UserId, "Marked Absent",
                $"You were marked absent for {date:yyyy-MM-dd} by management.");
        }

        await _auditService.LogAsync(actingUserId, "MarkAbsent", "Attendance", attendance.AttendanceId.ToString(),
            $"Marked employee {employeeId} absent for {date:yyyy-MM-dd}.", ipAddress);

        return ServiceResult.Ok();
    }

    public async Task<bool> HasScheduledShiftAsync(int employeeId, DateOnly date)
    {
        return await _db.Schedules.AnyAsync(s => s.EmployeeId == employeeId && s.ShiftDate == date);
    }

    public async Task<List<Attendance>> GetHistoryAsync(int employeeId, DateOnly? from, DateOnly? to)
    {
        var query = _db.Attendances.Where(a => a.EmployeeId == employeeId);
        if (from is not null)
        {
            query = query.Where(a => a.AttendanceDate >= from);
        }
        if (to is not null)
        {
            query = query.Where(a => a.AttendanceDate <= to);
        }

        return await query.OrderByDescending(a => a.AttendanceDate).ToListAsync();
    }

    public async Task<List<Attendance>> GetAllAsync(DateOnly? from, DateOnly? to)
    {
        var query = _db.Attendances.Include(a => a.Employee).AsQueryable();
        if (from is not null)
        {
            query = query.Where(a => a.AttendanceDate >= from);
        }
        if (to is not null)
        {
            query = query.Where(a => a.AttendanceDate <= to);
        }

        return await query.OrderByDescending(a => a.AttendanceDate).ToListAsync();
    }
}
