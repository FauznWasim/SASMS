using SASMS.Web.Models.Entities;
using SASMS.Web.Services.Dtos;

namespace SASMS.Web.Services;

public interface ILeaveService
{
    Task<ServiceResult<int>> SubmitAsync(SubmitLeaveApplicationInput input, int actingUserId, string? ipAddress);

    Task<List<LeaveApplication>> GetForEmployeeAsync(int employeeId);

    Task<List<LeaveApplication>> GetAllAsync();

    /// <summary>Count of applications currently awaiting Manager/PIC review — used by the
    /// Manager Dashboard's "Pending Leave" card. Approved/Rejected applications never count.</summary>
    Task<int> GetPendingCountAsync();

    /// <summary>All-or-nothing: creates an OnLeave/NotApplicable Attendance row for every
    /// scheduled date in the approved range, but only if NONE of those scheduled dates already
    /// has an Attendance row of any status. Never overwrites, deletes, or modifies an existing
    /// Attendance record. Schedule rows are never touched.</summary>
    Task<ServiceResult> ApproveAsync(int leaveApplicationId, int actingUserId, string? remarks, string? ipAddress);

    Task<ServiceResult> RejectAsync(int leaveApplicationId, int actingUserId, string? remarks, string? ipAddress);

    /// <summary>Resolves the on-disk path/content-type/download-name for a leave application's
    /// supporting document, but only if the requester is the owning employee or a Manager/PIC
    /// and an attachment actually exists — returns null for every other case (not found, no
    /// attachment, or not authorized), deliberately indistinguishable so a caller can't probe
    /// which LeaveApplicationIds exist.</summary>
    Task<(string PhysicalPath, string ContentType, string DownloadFileName)?> GetAttachmentFileInfoAsync(
        int leaveApplicationId, int? requestingEmployeeId, bool requesterIsManager);
}
