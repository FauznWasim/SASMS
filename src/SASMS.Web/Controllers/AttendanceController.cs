using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SASMS.Web.Models.ApiDtos;
using SASMS.Web.Models.Enums;
using SASMS.Web.Security;
using SASMS.Web.Services;
using SASMS.Web.Services.Dtos;

namespace SASMS.Web.Controllers;

[Authorize]
public class AttendanceController : Controller
{
    private static readonly HashSet<string> ValidCaptureModes = new(StringComparer.OrdinalIgnoreCase) { "checkin", "checkout", "register" };

    private readonly IAttendanceService _attendanceService;
    private readonly IEmployeeService _employeeService;
    private readonly IFaceVerificationClient _faceVerificationClient;
    private readonly ICurrentUserService _currentUser;
    private readonly IBusinessClock _businessClock;

    public AttendanceController(
        IAttendanceService attendanceService,
        IEmployeeService employeeService,
        IFaceVerificationClient faceVerificationClient,
        ICurrentUserService currentUser,
        IBusinessClock businessClock)
    {
        _attendanceService = attendanceService;
        _employeeService = employeeService;
        _faceVerificationClient = faceVerificationClient;
        _currentUser = currentUser;
        _businessClock = businessClock;
    }

    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to)
    {
        if (_currentUser.IsManager)
        {
            return RedirectToAction(nameof(All));
        }

        var employeeId = _currentUser.EmployeeId;
        if (employeeId is null)
        {
            return Forbid();
        }

        var history = await _attendanceService.GetHistoryAsync(employeeId.Value, from, to);
        return View(history);
    }

    /// <summary>Renders the webcam-capture page for check-in/check-out/registration. Available to
    /// both roles — Manager/PIC checks in/out for themselves too (README §6.2). Employees/
    /// Managers without a registered face template are redirected to register before check-in/out.</summary>
    [HttpGet]
    public async Task<IActionResult> Capture(string mode)
    {
        if (!ValidCaptureModes.Contains(mode))
        {
            return BadRequest();
        }

        var employeeId = _currentUser.EmployeeId;
        if (employeeId is null)
        {
            return Forbid();
        }

        // Check-in specifically must not even render the capture page once today's Attendance
        // record already exists — mirrors the same guard already enforced in the CheckIn POST
        // action (defense in depth, not a replacement for it). Scoped to "checkin" only so a
        // legitimate checkout capture for an already-Present employee is never blocked here.
        if (mode.Equals("checkin", StringComparison.OrdinalIgnoreCase))
        {
            var today = _businessClock.Today;
            var existingToday = (await _attendanceService.GetHistoryAsync(employeeId.Value, today, today)).FirstOrDefault();
            if (existingToday is not null)
            {
                TempData["StatusMessage"] = existingToday.AttendanceStatus == AttendanceStatus.OnLeave
                    ? "You are on approved leave today and cannot check in."
                    : "You have already checked in today.";
                return RedirectToAction(nameof(Index));
            }
        }

        if (!mode.Equals("register", StringComparison.OrdinalIgnoreCase))
        {
            var employee = await _employeeService.GetByIdAsync(employeeId.Value);
            if (employee?.FaceTemplateRef is null)
            {
                TempData["StatusMessage"] = "Register your face before you can check in or out.";
                return RedirectToAction(nameof(Capture), new { mode = "register" });
            }
        }

        ViewBag.Mode = mode.ToLowerInvariant();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn([FromBody] FaceFramesSubmission? submission)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId is null)
        {
            return Forbid();
        }

        var today = _businessClock.Today;
        var existing = (await _attendanceService.GetHistoryAsync(employeeId.Value, today, today)).FirstOrDefault();
        if (existing is not null)
        {
            TempData["StatusMessage"] = existing.AttendanceStatus == AttendanceStatus.OnLeave
                ? "You are on approved leave today and cannot check in."
                : "You have already checked in today.";
            return RedirectToAction(nameof(Index));
        }

        // The Schedule table is the sole authority for "expected to work today" — checked
        // before facial verification even starts, so a rest day never triggers the camera,
        // never creates an attendance record, and never counts against the attempt limit.
        if (!await _attendanceService.HasScheduledShiftAsync(employeeId.Value, today))
        {
            TempData["StatusMessage"] = "You are not scheduled to work today.";
            return RedirectToAction(nameof(Index));
        }

        var attemptStatus = await _attendanceService.GetAttemptStatusAsync(employeeId.Value);
        if (attemptStatus.CheckInBlocked)
        {
            TempData["StatusMessage"] = "Too many failed check-in attempts today. Ask your Manager/PIC to review.";
            return RedirectToAction(nameof(Index));
        }

        // Verify FIRST, and only ever record attendance on a real success (README §9: "Record
        // attendance only when verification succeeds") — no Pending row gets created just because
        // someone tried the camera; a failed attempt leaves no attendance trace at all, only an
        // audit-logged attempt (via RegisterFailedAttemptAsync below).
        var faceResult = await _faceVerificationClient.VerifyAsync(employeeId.Value, submission?.Frames ?? new List<string>());

        if (faceResult.Success && faceResult.FaceMatchSuccess && faceResult.LivenessPassed)
        {
            var result = await _attendanceService.RecordVerifiedCheckInAsync(employeeId.Value, _currentUser.UserId, _currentUser.IpAddress);
            TempData["StatusMessage"] = result.Success ? "Checked in and verified successfully." : result.Error;
            return RedirectToAction(nameof(Index));
        }

        var remaining = await _attendanceService.RegisterFailedAttemptAsync(employeeId.Value, isCheckIn: true, _currentUser.UserId, _currentUser.IpAddress);
        TempData["StatusMessage"] = DescribeFailure(faceResult, "Check-in") + " " + DescribeRemainingAttempts(remaining);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckOut([FromBody] FaceFramesSubmission? submission)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId is null)
        {
            return Forbid();
        }

        // A failed check-in must not be quietly "fixed" by a later successful check-out scan
        // — that would let a fraudulent check-in slide through as long as the real employee
        // shows up to check out, defeating the whole point of verifying check-in at all. A
        // Failed (or otherwise not-yet-Verified) day requires a Manager/PIC's manual override
        // before check-out is even attempted, same as it already requires for reporting.
        var today = _businessClock.Today;
        var todayRecord = (await _attendanceService.GetHistoryAsync(employeeId.Value, today, today)).FirstOrDefault();
        if (todayRecord is null)
        {
            TempData["StatusMessage"] = "You have not checked in today.";
            return RedirectToAction(nameof(Index));
        }

        if (todayRecord.VerificationStatus != VerificationStatus.Verified)
        {
            TempData["StatusMessage"] = "Your check-in was not verified, so you can't check out yet. Ask your Manager/PIC to review it first.";
            return RedirectToAction(nameof(Index));
        }

        if (todayRecord.CheckOutTime is not null)
        {
            TempData["StatusMessage"] = "You have already checked out today.";
            return RedirectToAction(nameof(Index));
        }

        var attemptStatus = await _attendanceService.GetAttemptStatusAsync(employeeId.Value);
        if (attemptStatus.CheckOutBlocked)
        {
            TempData["StatusMessage"] = "Too many failed check-out attempts today. Ask your Manager/PIC to review.";
            return RedirectToAction(nameof(Index));
        }

        // Check-out is also gated on its own live face+liveness capture (matches the doc's
        // own attendance workflow diagram, which shows verification on both paths) — but it's
        // a pass/fail gate on the action itself, not a second persisted VerificationStatus:
        // the schema only tracks one verification event per day, tied to check-in.
        var faceResult = await _faceVerificationClient.VerifyAsync(employeeId.Value, submission?.Frames ?? new List<string>());
        if (!faceResult.Success || !faceResult.FaceMatchSuccess || !faceResult.LivenessPassed)
        {
            var remaining = await _attendanceService.RegisterFailedAttemptAsync(employeeId.Value, isCheckIn: false, _currentUser.UserId, _currentUser.IpAddress);
            TempData["StatusMessage"] = DescribeFailure(faceResult, "Check-out") + " " + DescribeRemainingAttempts(remaining);
            return RedirectToAction(nameof(Index));
        }

        var result = await _attendanceService.CheckOutAsync(employeeId.Value, _currentUser.UserId, _currentUser.IpAddress);
        TempData["StatusMessage"] = result.Success ? "Checked out and verified successfully." : result.Error;

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterFace([FromBody] FaceFramesSubmission? submission)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId is null)
        {
            return Forbid();
        }

        var faceResult = await _faceVerificationClient.RegisterFaceAsync(employeeId.Value, submission?.Frames ?? new List<string>());
        if (!faceResult.Success)
        {
            TempData["StatusMessage"] = faceResult.Message;
            return RedirectToAction("Index", "Home");
        }

        await _employeeService.MarkFaceRegisteredAsync(employeeId.Value, _currentUser.UserId, _currentUser.IpAddress);
        TempData["StatusMessage"] = "Face registered. You can now check in and out with facial verification.";
        return RedirectToAction("Index", "Home");
    }

    private static string DescribeRemainingAttempts(int remaining)
    {
        return remaining > 0
            ? $"{remaining} attempt(s) remaining today."
            : "No attempts remaining today — you are now blocked. Ask your Manager/PIC to review.";
    }

    private static string DescribeFailure(FaceVerifyResult faceResult, string action)
    {
        if (!faceResult.Success)
        {
            return faceResult.ErrorMessage ?? $"{action} verification could not be completed. Ask your Manager/PIC for a manual check.";
        }

        if (!faceResult.FaceMatchSuccess)
        {
            return $"{action} failed: face did not match your registered profile. Ask your Manager/PIC for a manual check.";
        }

        if (!faceResult.LivenessPassed)
        {
            return $"{action} failed: liveness check did not detect a live person (no blink). Ask your Manager/PIC for a manual check.";
        }

        return $"{action} verified successfully.";
    }

    [Authorize(Roles = RoleNames.Manager)]
    public async Task<IActionResult> All(DateOnly? from, DateOnly? to)
    {
        var records = await _attendanceService.GetAllAsync(from, to);
        ViewBag.Employees = await _employeeService.GetAllAsync();
        return View(records);
    }

    [Authorize(Roles = RoleNames.Manager)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManualVerify(int attendanceId)
    {
        var result = await _attendanceService.ManualVerificationOverrideAsync(attendanceId, _currentUser.UserId, _currentUser.IpAddress);
        TempData["StatusMessage"] = result.Success
            ? "Marked Verified using the manual override (audit logged)."
            : result.Error;

        return RedirectToAction(nameof(All));
    }

    [Authorize(Roles = RoleNames.Manager)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManualCheckOut(int attendanceId)
    {
        var result = await _attendanceService.ManualCheckOutOverrideAsync(attendanceId, _currentUser.UserId, _currentUser.IpAddress);
        TempData["StatusMessage"] = result.Success
            ? "Marked checked-out using the manual override (audit logged)."
            : result.Error;

        return RedirectToAction(nameof(All));
    }

    [Authorize(Roles = RoleNames.Manager)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAbsent(int employeeId, DateOnly date)
    {
        var result = await _attendanceService.MarkAbsentAsync(employeeId, date, _currentUser.UserId, _currentUser.IpAddress);
        TempData["StatusMessage"] = result.Success ? "Marked absent." : result.Error;

        return RedirectToAction(nameof(All));
    }

    [Authorize(Roles = RoleNames.Manager)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManualCheckIn(int employeeId)
    {
        var result = await _attendanceService.ManualCheckInOverrideAsync(employeeId, _currentUser.UserId, _currentUser.IpAddress);
        TempData["StatusMessage"] = result.Success
            ? "Recorded a Verified check-in using the manual override (audit logged)."
            : result.Error;

        return RedirectToAction("Details", "Employees", new { id = employeeId });
    }

    [Authorize(Roles = RoleNames.Manager)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetAttempts(int employeeId)
    {
        var result = await _attendanceService.ResetVerificationAttemptsAsync(employeeId, _currentUser.UserId, _currentUser.IpAddress);
        TempData["StatusMessage"] = result.Success ? "Verification attempt counters reset." : result.Error;

        return RedirectToAction("Details", "Employees", new { id = employeeId });
    }
}
