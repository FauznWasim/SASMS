namespace SASMS.Web.Security;

/// <summary>Single source of truth for "what Malaysia business day/time is it" — every
/// attendance/schedule/report decision that represents a business calendar date must go
/// through this, never DateTime.UtcNow or DateTime.Today directly. True timestamps (audit
/// logs, CreatedAtUtc, CheckInTime/CheckOutTime) are a separate concern and correctly stay
/// as raw DateTime.UtcNow — this only centralizes the *business date* concept, so a server
/// deployed with any OS-default timezone (including UTC) still computes Malaysia business
/// dates correctly.</summary>
public interface IBusinessClock
{
    /// <summary>The current business date in Malaysia time (Asia/Kuala_Lumpur, UTC+8, no DST).</summary>
    DateOnly Today { get; }

    /// <summary>Converts a Malaysia wall-clock date+time — e.g. a Schedule's ShiftDate/StartTime,
    /// entered by a Manager who means local time — into the real UTC instant it represents, so
    /// it can be correctly compared against DateTime.UtcNow-based instants like CheckInTime.</summary>
    DateTime ConvertBusinessLocalToUtc(DateOnly date, TimeOnly time);

    /// <summary>Converts a stored UTC instant (e.g. Attendance.CheckInTime/CheckOutTime,
    /// Notification.CreatedAtUtc) into Malaysia wall-clock time for DISPLAY only — the stored
    /// value itself must stay UTC. Never use this for business-date decisions; use Today or
    /// ConvertBusinessLocalToUtc for those instead.</summary>
    DateTime ToMalaysiaTime(DateTime utcInstant);
}
