using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SASMS.Web.Models.Enums;
using SASMS.Web.Models.ViewModels;
using SASMS.Web.Security;
using SASMS.Web.Services;
using SASMS.Web.Services.Dtos;

namespace SASMS.Web.Controllers;

[Authorize(Roles = RoleNames.Manager)]
public class EmployeesController : Controller
{
    private readonly IEmployeeService _employeeService;
    private readonly IAttendanceService _attendanceService;
    private readonly ICurrentUserService _currentUser;
    private readonly IBusinessClock _businessClock;

    public EmployeesController(IEmployeeService employeeService, IAttendanceService attendanceService, ICurrentUserService currentUser, IBusinessClock businessClock)
    {
        _employeeService = employeeService;
        _attendanceService = attendanceService;
        _currentUser = currentUser;
        _businessClock = businessClock;
    }

    public async Task<IActionResult> Index()
    {
        var employees = await _employeeService.GetAllAsync();
        return View(employees);
    }

    public async Task<IActionResult> Details(int id)
    {
        var employee = await _employeeService.GetByIdAsync(id);
        if (employee is null)
        {
            return NotFound();
        }

        var today = _businessClock.Today;
        var todayRecord = (await _attendanceService.GetHistoryAsync(id, today, today)).FirstOrDefault();
        var attemptStatus = await _attendanceService.GetAttemptStatusAsync(id);

        var model = new EmployeeDetailsViewModel
        {
            Employee = employee,
            AttemptStatus = attemptStatus,
            HasTodayRecord = todayRecord is not null
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult Create() => View(new EmployeeCreateViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmployeeCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var input = new CreateEmployeeInput(model.FullName, model.Email, model.Phone, model.Position, model.Department, model.Username, model.Role);
        var result = await _employeeService.CreateAsync(input, _currentUser.UserId, _currentUser.IpAddress);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        TempData["StatusMessage"] =
            $"Employee created. Temporary login password for '{model.Username}': {result.Data!.TemporaryPassword} " +
            "(shown once only — hand it to the employee and have them change it after first login).";

        return RedirectToAction(nameof(Details), new { id = result.Data!.EmployeeId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var employee = await _employeeService.GetByIdAsync(id);
        if (employee is null)
        {
            return NotFound();
        }

        var model = new EmployeeEditViewModel
        {
            EmployeeId = employee.EmployeeId,
            FullName = employee.FullName,
            Email = employee.Email,
            // Legacy records created before Phone became compulsory may still have none —
            // surfaced as an empty (and now invalid) field, so saving the form forces it in.
            Phone = employee.Phone ?? string.Empty,
            Position = employee.Position,
            Department = employee.Department
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EmployeeEditViewModel model)
    {
        if (id != model.EmployeeId)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var input = new UpdateEmployeeInput(model.FullName, model.Email, model.Phone, model.Position, model.Department);
        var result = await _employeeService.UpdateAsync(id, input, _currentUser.UserId, _currentUser.IpAddress);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        TempData["StatusMessage"] = "Employee updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        var result = await _employeeService.DeactivateAsync(id, _currentUser.UserId, _currentUser.IpAddress);
        TempData["StatusMessage"] = result.Success ? "Employee deactivated." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(int id)
    {
        var result = await _employeeService.ReactivateAsync(id, _currentUser.UserId, _currentUser.IpAddress);
        TempData["StatusMessage"] = result.Success ? "Employee reactivated." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var result = await _employeeService.ResetPasswordAsync(id, _currentUser.UserId, _currentUser.IpAddress);
        TempData["StatusMessage"] = result.Success
            ? $"Password reset. New temporary password: {result.Data} (shown once only — hand it to them and have them change it after logging in)."
            : result.Error;

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetFaceRegistration(int id)
    {
        var result = await _employeeService.ResetFaceRegistrationAsync(id, _currentUser.UserId, _currentUser.IpAddress);
        TempData["StatusMessage"] = result.Success
            ? "Face registration reset. The employee will need to register their face again before they can check in or out."
            : result.Error;

        return RedirectToAction(nameof(Details), new { id });
    }
}
