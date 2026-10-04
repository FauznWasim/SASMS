using SASMS.Web.Models.Entities;
using SASMS.Web.Services.Dtos;

namespace SASMS.Web.Services;

public interface IAttendanceService
{
    /// <summary>Creates a Verified attendance row directly. Call ONLY after face+liveness
    /// verification has actually succeeded — per the doc's own requirement (README §9),
    /// attendance is recorded on success only, never speculatively ahead of the result.</summary>
    Task<ServiceResult<Attendance>> RecordVerifiedCheckInAsync(int employeeId, int actingUserId, string? ipAddress);

    Task<ServiceResult> CheckOutAsync(int employeeId, int actingUserId, string? ipAddress);

    /// <summary>Called via the authenticated API callback (Api/VerificationController) for an
    /// external/decoupled verification caller — not used by the interactive check-in/out flow,
    /// which applies its result in-process instead.</summary>
    Task<ServiceResult> ApplyVerificationResultAsync(int employeeId, DateOnly attendanceDate, bool success, bool livenessPassed);

    /// <summary>Manager/PIC-only, audited, test-only stand-in for a Pending/Failed record (e.g. from
    /// the external callback path) that needs to be resolved without a repeat live scan.</summary>
    Task<ServiceResult> ManualVerificationOverrideAsync(int attendanceId, int actingUserId, string? ipAddress);

    /// <summary>Manager/PIC-only, audited, test-only escape hatch for an employee whose check-in was
    /// verified (real or manual) but whose check-out keeps failing the live face scan.</summary>
    Task<ServiceResult> ManualCheckOutOverrideAsync(int attendanceId, int actingUserId, string? ipAddress);

    /// <summary>Manager/PIC-only, audited, test-only escape hatch for an employee who has no
    /// attendance record yet today — typically because they're blocked after too many failed
    /// live check-in attempts. Creates a Verified row directly, bypassing the camera entirely.</summary>
    Task<ServiceResult> ManualCheckInOverrideAsync(int employeeId, int actingUserId, string? ipAddress);

    Task<ServiceResult> MarkAbsentAsync(int employeeId, DateOnly date, int actingUserId, string? ipAddress);

    /// <summary>The Schedule table is the sole authority for whether an employee is expected to
    /// work a given date — true only when a real Schedule row exists for that employee/date.
    /// No schedule means a rest day: no check-in, no Manual Absent, no attendance record at all.</summary>
    Task<bool> HasScheduledShiftAsync(int employeeId, DateOnly date);

    Task<List<Attendance>> GetHistoryAsync(int employeeId, DateOnly? from, DateOnly? to);
    Task<List<Attendance>> GetAllAsync(DateOnly? from, DateOnly? to);

    /// <summary>Records a failed live-verification attempt (check-in or check-out) and returns how
    /// many attempts remain today (0 = now blocked). Counters auto-reset on a new day.</summary>
    Task<int> RegisterFailedAttemptAsync(int employeeId, bool isCheckIn, int actingUserId, string? ipAddress);

    Task<AttemptStatus> GetAttemptStatusAsync(int employeeId);

    /// <summary>Manager/PIC-only, audited: clears today's failed-attempt counters so a blocked
    /// employee can try the live scan again (still requires an actual successful verification —
    /// this resets the retry budget, it doesn't itself grant access).</summary>
    Task<ServiceResult> ResetVerificationAttemptsAsync(int employeeId, int actingUserId, string? ipAddress);
}
