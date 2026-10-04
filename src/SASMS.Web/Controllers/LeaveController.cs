using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SASMS.Web.Models.ViewModels;
using SASMS.Web.Security;
using SASMS.Web.Services;
using SASMS.Web.Services.Dtos;

namespace SASMS.Web.Controllers;

[Authorize]
public class LeaveController : Controller
{
    private readonly ILeaveService _leaveService;
    private readonly ICurrentUserService _currentUser;
    private readonly IBusinessClock _businessClock;

    public LeaveController(ILeaveService leaveService, ICurrentUserService currentUser, IBusinessClock businessClock)
    {
        _leaveService = leaveService;
        _currentUser = currentUser;
        _businessClock = businessClock;
    }

    /// <summary>Employee sees their own applications; Manager/PIC is redirected to Review —
    /// same role-branch pattern as AttendanceController.Index/SchedulesController.Index.</summary>
    public async Task<IActionResult> Index()
    {
        if (_currentUser.IsManager)
        {
            return RedirectToAction(nameof(Review));
        }

        var employeeId = _currentUser.EmployeeId;
        if (employeeId is null)
        {
            return Forbid();
        }

        var applications = await _leaveService.GetForEmployeeAsync(employeeId.Value);
        return View(applications);
    }

    [Authorize(Roles = RoleNames.Employee)]
    [HttpGet]
    public IActionResult Apply() => View(new LeaveApplyViewModel
    {
        StartDate = _businessClock.Today,
        EndDate = _businessClock.Today
    });

    [Authorize(Roles = RoleNames.Employee)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(LeaveApplyViewModel model)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId is null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var input = new SubmitLeaveApplicationInput(
            employeeId.Value, model.StartDate, model.EndDate, model.Reason,
            model.LeaveType!.Value, model.SupportingDocument);
        var result = await _leaveService.SubmitAsync(input, _currentUser.UserId, _currentUser.IpAddress);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        TempData["StatusMessage"] = "Leave application submitted.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Serves a leave application's supporting document — only to the owning
    /// employee or a Manager/PIC. Returns the same 404 whether the application doesn't exist,
    /// has no attachment, or the requester isn't authorized, so a request can't distinguish
    /// "doesn't exist" from "not yours to see."</summary>
    [HttpGet]
    public async Task<IActionResult> Attachment(int id)
    {
        var info = await _leaveService.GetAttachmentFileInfoAsync(id, _currentUser.EmployeeId, _currentUser.IsManager);
        if (info is null)
        {
            return NotFound();
        }

        return PhysicalFile(info.Value.PhysicalPath, info.Value.ContentType, info.Value.DownloadFileName);
    }

    [Authorize(Roles = RoleNames.Manager)]
    public async Task<IActionResult> Review()
    {
        var applications = await _leaveService.GetAllAsync();
        return View(applications);
    }

    [Authorize(Roles = RoleNames.Manager)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? remarks)
    {
        var result = await _leaveService.ApproveAsync(id, _currentUser.UserId, remarks, _currentUser.IpAddress);
        TempData["StatusMessage"] = result.Success ? "Leave application approved." : result.Error;
        return RedirectToAction(nameof(Review));
    }

    [Authorize(Roles = RoleNames.Manager)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? remarks)
    {
        var result = await _leaveService.RejectAsync(id, _currentUser.UserId, remarks, _currentUser.IpAddress);
        TempData["StatusMessage"] = result.Success ? "Leave application rejected." : result.Error;
        return RedirectToAction(nameof(Review));
    }
}
