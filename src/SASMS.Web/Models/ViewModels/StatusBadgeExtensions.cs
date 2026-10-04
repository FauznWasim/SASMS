using SASMS.Web.Models.Enums;

namespace SASMS.Web.Models.ViewModels;

/// <summary>Variant maps to a `.status-badge--{variant}` CSS class in site.css.</summary>
public record StatusBadgeViewModel(string Text, string Variant);

public static class StatusBadgeExtensions
{
    public static StatusBadgeViewModel ToBadge(this AttendanceStatus status) => status switch
    {
        AttendanceStatus.Present => new StatusBadgeViewModel("Present", "good"),
        AttendanceStatus.Late => new StatusBadgeViewModel("Late", "warning"),
        AttendanceStatus.OnLeave => new StatusBadgeViewModel("On Leave", "serious"),
        AttendanceStatus.Absent => new StatusBadgeViewModel("Absent", "critical"),
        _ => new StatusBadgeViewModel(status.ToString(), "neutral")
    };

    public static StatusBadgeViewModel ToBadge(this VerificationStatus status) => status switch
    {
        VerificationStatus.Verified => new StatusBadgeViewModel("Verified", "good"),
        VerificationStatus.Pending => new StatusBadgeViewModel("Pending", "warning"),
        VerificationStatus.Failed => new StatusBadgeViewModel("Failed", "critical"),
        VerificationStatus.NotApplicable => new StatusBadgeViewModel("N/A", "neutral"),
        _ => new StatusBadgeViewModel(status.ToString(), "neutral")
    };

    public static StatusBadgeViewModel ToBadge(this EmploymentStatus status) => status switch
    {
        EmploymentStatus.Active => new StatusBadgeViewModel("Active", "good"),
        EmploymentStatus.Inactive => new StatusBadgeViewModel("Inactive", "neutral"),
        _ => new StatusBadgeViewModel(status.ToString(), "neutral")
    };

    public static StatusBadgeViewModel ToBadge(this LeaveStatus status) => status switch
    {
        LeaveStatus.Pending => new StatusBadgeViewModel("Pending", "warning"),
        LeaveStatus.Approved => new StatusBadgeViewModel("Approved", "good"),
        LeaveStatus.Rejected => new StatusBadgeViewModel("Rejected", "critical"),
        _ => new StatusBadgeViewModel(status.ToString(), "neutral")
    };
}
