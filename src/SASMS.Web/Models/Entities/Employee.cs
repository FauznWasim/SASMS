using SASMS.Web.Models.Enums;

namespace SASMS.Web.Models.Entities;

public class Employee
{
    public int EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Position { get; set; } = string.Empty;
    public string? Department { get; set; }
    public EmploymentStatus EmploymentStatus { get; set; } = EmploymentStatus.Active;
    public DateTime DateJoined { get; set; } = DateTime.UtcNow;

    /// <summary>Marker for "has this employee registered a face template?" — the actual biometric
    /// data lives in face-service's own storage, not this DB. Set by MarkFaceRegisteredAsync.</summary>
    public string? FaceTemplateRef { get; set; }

    /// <summary>Failed live-verification attempts today (check-in and check-out tracked separately,
    /// each capped at AttendanceService.MaxAttemptsPerDay). Reset automatically when
    /// AttemptsResetDate rolls to a new day — see AttendanceService.GetAttemptStatusAsync.</summary>
    public int FailedCheckInAttempts { get; set; }
    public int FailedCheckOutAttempts { get; set; }
    public DateOnly? AttemptsResetDate { get; set; }

    public User? User { get; set; }
    public ICollection<Attendance> AttendanceRecords { get; set; } = new List<Attendance>();
    public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
}
