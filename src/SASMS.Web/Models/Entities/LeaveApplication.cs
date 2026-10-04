using SASMS.Web.Models.Enums;

namespace SASMS.Web.Models.Entities;

public class LeaveApplication
{
    public int LeaveApplicationId { get; set; }

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public LeaveType LeaveType { get; set; } = LeaveType.Other;

    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public DateTime AppliedAtUtc { get; set; } = DateTime.UtcNow;

    public int? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewRemarks { get; set; }

    /// <summary>The file's actual name on disk (a random GUID + validated extension, never
    /// the employee's original filename) under App_Data/LeaveAttachments/ — outside wwwroot,
    /// so it's never reachable by guessing a URL. Null when no document was submitted.</summary>
    public string? AttachmentStoredFileName { get; set; }

    /// <summary>The employee's original uploaded filename, kept only for display/download —
    /// never used to build a file-system path.</summary>
    public string? AttachmentOriginalFileName { get; set; }
}
