using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using SASMS.Web.Data;
using SASMS.Web.Models.Entities;
using SASMS.Web.Models.Enums;
using SASMS.Web.Security;
using SASMS.Web.Services.Dtos;

namespace SASMS.Web.Services;

public class LeaveService : ILeaveService
{
    // Deliberately outside wwwroot — app.UseStaticFiles() only ever serves wwwroot, so a
    // file saved here can never be reached by guessing a URL; the only way in is through
    // LeaveController.Attachment's own owner-or-manager authorization check.
    private const string AttachmentsFolderName = "LeaveAttachments";
    private const long MaxAttachmentBytes = 500 * 1024;

    // (extension -> file-signature bytes). JPG and JPEG are the same format/signature, listed
    // separately only because both extensions are allowed. Checked against the file's actual
    // bytes, not just its claimed extension — a renamed file won't pass this.
    private static readonly Dictionary<string, byte[]> AllowedAttachmentSignatures = new()
    {
        [".jpg"] = new byte[] { 0xFF, 0xD8, 0xFF },
        [".jpeg"] = new byte[] { 0xFF, 0xD8, 0xFF },
        [".pdf"] = new byte[] { 0x25, 0x50, 0x44, 0x46 } // "%PDF"
    };

    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;
    private readonly IBusinessClock _businessClock;
    private readonly string _attachmentsDirectory;

    public LeaveService(
        ApplicationDbContext db,
        IAuditService auditService,
        INotificationService notificationService,
        IBusinessClock businessClock,
        IWebHostEnvironment env)
    {
        _db = db;
        _auditService = auditService;
        _notificationService = notificationService;
        _businessClock = businessClock;
        _attachmentsDirectory = Path.Combine(env.ContentRootPath, "App_Data", AttachmentsFolderName);
    }

    public async Task<ServiceResult<int>> SubmitAsync(SubmitLeaveApplicationInput input, int actingUserId, string? ipAddress)
    {
        if (input.EndDate < input.StartDate)
        {
            return ServiceResult<int>.Fail("End date must be on or after the start date.");
        }

        if (input.StartDate < _businessClock.Today)
        {
            return ServiceResult<int>.Fail("You cannot apply for leave starting in the past.");
        }

        // Overlap against the employee's own Pending/Approved applications only — a Rejected
        // application must never block a new request for the same or overlapping dates.
        var overlaps = await _db.LeaveApplications.AnyAsync(l =>
            l.EmployeeId == input.EmployeeId &&
            (l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.Approved) &&
            l.StartDate <= input.EndDate && l.EndDate >= input.StartDate);
        if (overlaps)
        {
            return ServiceResult<int>.Fail("You already have a pending or approved leave application that overlaps these dates.");
        }

        string? storedFileName = null;
        string? originalFileName = null;
        if (input.SupportingDocument is not null)
        {
            var (ok, error, extension) = await ValidateAttachmentAsync(input.SupportingDocument);
            if (!ok)
            {
                return ServiceResult<int>.Fail(error!);
            }

            // Saved to disk only after every other validation has already passed, and named
            // with a random GUID (never the employee's own filename) to avoid path-traversal/
            // collision/information-disclosure risks from trusting client-supplied names.
            Directory.CreateDirectory(_attachmentsDirectory);
            storedFileName = $"{Guid.NewGuid()}{extension}";
            var fullPath = Path.Combine(_attachmentsDirectory, storedFileName);
            await using (var sourceStream = input.SupportingDocument.OpenReadStream())
            await using (var destinationStream = File.Create(fullPath))
            {
                await sourceStream.CopyToAsync(destinationStream);
            }
            originalFileName = Path.GetFileName(input.SupportingDocument.FileName);
        }

        var leave = new LeaveApplication
        {
            EmployeeId = input.EmployeeId,
            StartDate = input.StartDate,
            EndDate = input.EndDate,
            Reason = input.Reason,
            LeaveType = input.LeaveType,
            Status = LeaveStatus.Pending,
            AppliedAtUtc = DateTime.UtcNow,
            AttachmentStoredFileName = storedFileName,
            AttachmentOriginalFileName = originalFileName
        };
        _db.LeaveApplications.Add(leave);
        await _db.SaveChangesAsync();

        // Notify every active Manager/PIC — there's no single designated approver, so all of
        // them need to know a new application is awaiting review. Only reached once the save
        // above has actually succeeded, so a validation failure or DB error never notifies.
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == input.EmployeeId);
        var managerUserIds = await _db.Users
            .Where(u => u.Role.RoleName == RoleNames.Manager && u.Status == UserStatus.Active)
            .Select(u => u.UserId)
            .ToListAsync();
        foreach (var managerUserId in managerUserIds)
        {
            await _notificationService.NotifyAsync(managerUserId, "New Leave Application",
                $"{employee?.FullName ?? "An employee"} submitted a leave application for " +
                $"{input.StartDate:yyyy-MM-dd} to {input.EndDate:yyyy-MM-dd} and it is awaiting your review.");
        }

        await _auditService.LogAsync(actingUserId, "SubmitLeaveApplication", "LeaveApplication", leave.LeaveApplicationId.ToString(),
            $"Submitted leave from {input.StartDate:yyyy-MM-dd} to {input.EndDate:yyyy-MM-dd}.", ipAddress);

        return ServiceResult<int>.Ok(leave.LeaveApplicationId);
    }

    public async Task<List<LeaveApplication>> GetForEmployeeAsync(int employeeId)
    {
        return await _db.LeaveApplications
            .Include(l => l.ReviewedByUser)
            .Where(l => l.EmployeeId == employeeId)
            .OrderByDescending(l => l.AppliedAtUtc)
            .ToListAsync();
    }

    public async Task<List<LeaveApplication>> GetAllAsync()
    {
        return await _db.LeaveApplications
            .Include(l => l.Employee)
            .Include(l => l.ReviewedByUser)
            .OrderByDescending(l => l.AppliedAtUtc)
            .ToListAsync();
    }

    public async Task<int> GetPendingCountAsync()
    {
        return await _db.LeaveApplications.CountAsync(l => l.Status == LeaveStatus.Pending);
    }

    public async Task<ServiceResult> ApproveAsync(int leaveApplicationId, int actingUserId, string? remarks, string? ipAddress)
    {
        var leave = await _db.LeaveApplications
            .Include(l => l.Employee).ThenInclude(e => e.User)
            .FirstOrDefaultAsync(l => l.LeaveApplicationId == leaveApplicationId);
        if (leave is null)
        {
            return ServiceResult.Fail("Leave application not found.");
        }

        if (leave.Status != LeaveStatus.Pending)
        {
            return ServiceResult.Fail("This leave application has already been reviewed.");
        }

        // Only dates the employee actually has a Schedule row for are ever affected — a rest
        // day inside the requested range gets no Attendance row and no conflict check at all.
        var scheduledDates = await _db.Schedules
            .Where(s => s.EmployeeId == leave.EmployeeId && s.ShiftDate >= leave.StartDate && s.ShiftDate <= leave.EndDate)
            .Select(s => s.ShiftDate)
            .ToListAsync();

        if (scheduledDates.Count > 0)
        {
            // ANY existing Attendance row on a scheduled date — Present, Late, Absent, OnLeave,
            // or anything else — blocks the entire approval. Never overwritten, never deleted.
            var conflictingDates = await _db.Attendances
                .Where(a => a.EmployeeId == leave.EmployeeId && scheduledDates.Contains(a.AttendanceDate))
                .Select(a => a.AttendanceDate)
                .ToListAsync();
            if (conflictingDates.Count > 0)
            {
                var dateList = string.Join(", ", conflictingDates.OrderBy(d => d).Select(d => d.ToString("yyyy-MM-dd")));
                return ServiceResult.Fail($"Cannot approve — attendance already exists on: {dateList}. Resolve those records first, or reject this application.");
            }
        }

        foreach (var date in scheduledDates)
        {
            _db.Attendances.Add(new Attendance
            {
                EmployeeId = leave.EmployeeId,
                AttendanceDate = date,
                AttendanceStatus = AttendanceStatus.OnLeave,
                VerificationStatus = VerificationStatus.NotApplicable,
                CheckInTime = null,
                CheckOutTime = null,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        leave.Status = LeaveStatus.Approved;
        leave.ReviewedByUserId = actingUserId;
        leave.ReviewedAtUtc = DateTime.UtcNow;
        leave.ReviewRemarks = remarks;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (DbConcurrencyHelper.IsDuplicateKeyViolation(ex))
        {
            // A concurrent check-in/Mark Absent/other approval raced us between the conflict
            // check above and this save — fail cleanly rather than leaving a partial write.
            return ServiceResult.Fail("Cannot approve — an attendance record was just created on one of the scheduled dates. Please try again.");
        }

        if (leave.Employee.User is not null)
        {
            await _notificationService.NotifyAsync(leave.Employee.User.UserId, "Leave Approved",
                $"Your leave from {leave.StartDate:yyyy-MM-dd} to {leave.EndDate:yyyy-MM-dd} was approved.");
        }

        await _auditService.LogAsync(actingUserId, "ApproveLeaveApplication", "LeaveApplication", leave.LeaveApplicationId.ToString(),
            $"Approved leave for employee {leave.EmployeeId}; created {scheduledDates.Count} OnLeave attendance record(s).", ipAddress);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RejectAsync(int leaveApplicationId, int actingUserId, string? remarks, string? ipAddress)
    {
        var leave = await _db.LeaveApplications
            .Include(l => l.Employee).ThenInclude(e => e.User)
            .FirstOrDefaultAsync(l => l.LeaveApplicationId == leaveApplicationId);
        if (leave is null)
        {
            return ServiceResult.Fail("Leave application not found.");
        }

        if (leave.Status != LeaveStatus.Pending)
        {
            return ServiceResult.Fail("This leave application has already been reviewed.");
        }

        leave.Status = LeaveStatus.Rejected;
        leave.ReviewedByUserId = actingUserId;
        leave.ReviewedAtUtc = DateTime.UtcNow;
        leave.ReviewRemarks = remarks;
        await _db.SaveChangesAsync();

        if (leave.Employee.User is not null)
        {
            await _notificationService.NotifyAsync(leave.Employee.User.UserId, "Leave Rejected",
                $"Your leave from {leave.StartDate:yyyy-MM-dd} to {leave.EndDate:yyyy-MM-dd} was rejected." +
                (string.IsNullOrWhiteSpace(remarks) ? "" : $" Remarks: {remarks}"));
        }

        await _auditService.LogAsync(actingUserId, "RejectLeaveApplication", "LeaveApplication", leave.LeaveApplicationId.ToString(),
            $"Rejected leave for employee {leave.EmployeeId}.", ipAddress);

        return ServiceResult.Ok();
    }

    public async Task<(string PhysicalPath, string ContentType, string DownloadFileName)?> GetAttachmentFileInfoAsync(
        int leaveApplicationId, int? requestingEmployeeId, bool requesterIsManager)
    {
        var leave = await _db.LeaveApplications.FirstOrDefaultAsync(l => l.LeaveApplicationId == leaveApplicationId);
        if (leave?.AttachmentStoredFileName is null)
        {
            return null;
        }

        var isOwner = requestingEmployeeId is not null && requestingEmployeeId == leave.EmployeeId;
        if (!isOwner && !requesterIsManager)
        {
            return null;
        }

        var physicalPath = Path.Combine(_attachmentsDirectory, leave.AttachmentStoredFileName);
        if (!File.Exists(physicalPath))
        {
            return null;
        }

        var contentType = Path.GetExtension(leave.AttachmentStoredFileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
        var downloadFileName = leave.AttachmentOriginalFileName ?? leave.AttachmentStoredFileName;

        return (physicalPath, contentType, downloadFileName);
    }

    /// <summary>Server-side-only validation, independent of the client — checks the real
    /// file size and a magic-byte signature against the actual content, not just the claimed
    /// extension, so a renamed/mislabeled file can't pass. Returns the validated lowercase
    /// extension (including the dot) on success, for the caller to use when naming the
    /// stored file.</summary>
    private static async Task<(bool Ok, string? Error, string? Extension)> ValidateAttachmentAsync(IFormFile file)
    {
        if (file.Length == 0)
        {
            return (false, "The supporting document is empty.", null);
        }

        if (file.Length > MaxAttachmentBytes)
        {
            return (false, "The supporting document must be 500 KB or smaller.", null);
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedAttachmentSignatures.TryGetValue(extension, out var signature))
        {
            return (false, "The supporting document must be a JPG, JPEG, or PDF file.", null);
        }

        var header = new byte[signature.Length];
        await using var stream = file.OpenReadStream();
        var bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length));
        if (bytesRead < signature.Length || !header.AsSpan(0, signature.Length).SequenceEqual(signature))
        {
            return (false, "The supporting document's content does not match a valid JPG/JPEG/PDF file.", null);
        }

        return (true, null, extension);
    }
}
