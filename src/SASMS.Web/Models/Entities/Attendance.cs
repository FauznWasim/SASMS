using SASMS.Web.Models.Enums;

namespace SASMS.Web.Models.Entities;

public class Attendance
{
    public int AttendanceId { get; set; }

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateOnly AttendanceDate { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }

    public AttendanceStatus AttendanceStatus { get; set; } = AttendanceStatus.Present;

    /// <summary>Gates whether this record counts as a real, verified attendance event. Only
    /// finalized to Verified by a real facial-verification result or an explicitly audited
    /// Manager/PIC manual override. Never treat a Pending record as attendance.</summary>
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
