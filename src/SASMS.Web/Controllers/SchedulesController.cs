using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SASMS.Web.Models.ViewModels;
using SASMS.Web.Security;
using SASMS.Web.Services;
using SASMS.Web.Services.Dtos;

namespace SASMS.Web.Controllers;

[Authorize]
public class SchedulesController : Controller
{
    private readonly IScheduleService _scheduleService;
    private readonly IEmployeeService _employeeService;
    private readonly ICurrentUserService _currentUser;
    private readonly IBusinessClock _businessClock;

    public SchedulesController(IScheduleService scheduleService, IEmployeeService employeeService, ICurrentUserService currentUser, IBusinessClock businessClock)
    {
        _scheduleService = scheduleService;
        _employeeService = employeeService;
        _currentUser = currentUser;
        _businessClock = businessClock;
    }

    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to)
    {
        if (_currentUser.IsManager)
        {
            var all = await _scheduleService.GetAllAsync(from, to);
            return View("ManagerIndex", all);
        }

        var employeeId = _currentUser.EmployeeId;
        if (employeeId is null)
        {
            return Forbid();
        }

        var mine = await _scheduleService.GetByEmployeeAsync(employeeId.Value, from, to);
        return View("EmployeeIndex", mine);
    }

    [Authorize(Roles = RoleNames.Manager)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Employees = await _employeeService.GetAllAsync();
        return View(new ScheduleCreateViewModel { ShiftDate = _businessClock.Today });
    }

    [Authorize(Roles = RoleNames.Manager)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ScheduleCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Employees = await _employeeService.GetAllAsync();
            return View(model);
        }

        var input = new CreateScheduleInput(model.EmployeeId, model.ShiftDate, model.StartTime, model.EndTime, model.Notes);
        var result = await _scheduleService.CreateAsync(input, _currentUser.UserId, _currentUser.IpAddress);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            ViewBag.Employees = await _employeeService.GetAllAsync();
            return View(model);
        }

        TempData["StatusMessage"] = "Schedule created.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = RoleNames.Manager)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var schedule = await _scheduleService.GetByIdAsync(id);
        if (schedule is null)
        {
            return NotFound();
        }

        var model = new ScheduleEditViewModel
        {
            ScheduleId = schedule.ScheduleId,
            EmployeeName = schedule.Employee.FullName,
            ShiftDate = schedule.ShiftDate,
            StartTime = schedule.StartTime,
            EndTime = schedule.EndTime,
            Notes = schedule.Notes
        };

        return View(model);
    }

    [Authorize(Roles = RoleNames.Manager)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ScheduleEditViewModel model)
    {
        if (id != model.ScheduleId)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var input = new UpdateScheduleInput(model.ShiftDate, model.StartTime, model.EndTime, model.Notes);
        var result = await _scheduleService.UpdateAsync(id, input, _currentUser.UserId, _currentUser.IpAddress);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        TempData["StatusMessage"] = "Schedule updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = RoleNames.Manager)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _scheduleService.DeleteAsync(id, _currentUser.UserId, _currentUser.IpAddress);
        TempData["StatusMessage"] = result.Success ? "Schedule deleted." : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
