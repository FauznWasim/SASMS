using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SASMS.Web.Models.ViewModels;
using SASMS.Web.Security;
using SASMS.Web.Services;
using SASMS.Web.Services.Dtos;

namespace SASMS.Web.Controllers;

[Authorize(Roles = RoleNames.Manager)]
public class ReportsController : Controller
{
    private readonly IReportService _reportService;
    private readonly IBusinessClock _businessClock;

    public ReportsController(IReportService reportService, IBusinessClock businessClock)
    {
        _reportService = reportService;
        _businessClock = businessClock;
    }

    public IActionResult Index()
    {
        var today = _businessClock.Today;
        return View(new ReportFilterViewModel
        {
            From = today.AddDays(-30),
            To = today,
            Year = today.Year,
            Month = today.Month
        });
    }

    public async Task<IActionResult> Monthly(int? year, int? month)
    {
        var today = _businessClock.Today;
        var y = year ?? today.Year;
        var m = month ?? today.Month;
        ViewBag.Year = y;
        ViewBag.Month = m;
        return View(await _reportService.GetMonthlyAsync(y, m));
    }

    public async Task<IActionResult> MonthlyExport(int? year, int? month)
    {
        var today = _businessClock.Today;
        var y = year ?? today.Year;
        var m = month ?? today.Month;
        var rows = await _reportService.GetMonthlyAsync(y, m);
        return File(CsvExportHelper.ToCsv(rows), "text/csv", $"monthly-attendance-{y}-{m:00}.csv");
    }

    public async Task<IActionResult> Late(DateOnly? from, DateOnly? to)
    {
        var (f, t) = NormalizeRange(from, to, _businessClock.Today);
        ViewBag.From = f;
        ViewBag.To = t;
        return View(await _reportService.GetLateAsync(f, t));
    }

    public async Task<IActionResult> LateExport(DateOnly? from, DateOnly? to)
    {
        var (f, t) = NormalizeRange(from, to, _businessClock.Today);
        var rows = await _reportService.GetLateAsync(f, t);
        // CheckInTime is converted to Malaysia local time here, at the export edge — GetLateAsync
        // itself stays UTC (it's business/data logic, not a display concern).
        var exportRows = rows.Select(r => new LateAttendanceExportRow(
            r.AttendanceId,
            r.EmployeeName,
            r.AttendanceDate,
            r.CheckInTime is not null ? _businessClock.ToMalaysiaTime(r.CheckInTime.Value) : null,
            r.MinutesLate));
        return File(CsvExportHelper.ToCsv(exportRows), "text/csv", $"late-attendance-{f}-{t}.csv");
    }

    public async Task<IActionResult> Absence(DateOnly? from, DateOnly? to)
    {
        var (f, t) = NormalizeRange(from, to, _businessClock.Today);
        ViewBag.From = f;
        ViewBag.To = t;
        return View(await _reportService.GetAbsenceAsync(f, t));
    }

    public async Task<IActionResult> AbsenceExport(DateOnly? from, DateOnly? to)
    {
        var (f, t) = NormalizeRange(from, to, _businessClock.Today);
        var rows = await _reportService.GetAbsenceAsync(f, t);
        return File(CsvExportHelper.ToCsv(rows), "text/csv", $"absence-{f}-{t}.csv");
    }

    public async Task<IActionResult> Overtime(DateOnly? from, DateOnly? to)
    {
        var (f, t) = NormalizeRange(from, to, _businessClock.Today);
        ViewBag.From = f;
        ViewBag.To = t;
        return View(await _reportService.GetOvertimeAsync(f, t));
    }

    public async Task<IActionResult> OvertimeExport(DateOnly? from, DateOnly? to)
    {
        var (f, t) = NormalizeRange(from, to, _businessClock.Today);
        var rows = await _reportService.GetOvertimeAsync(f, t);
        return File(CsvExportHelper.ToCsv(rows), "text/csv", $"overtime-{f}-{t}.csv");
    }

    private static (DateOnly From, DateOnly To) NormalizeRange(DateOnly? from, DateOnly? to, DateOnly businessToday)
    {
        var t = to ?? businessToday;
        var f = from ?? t.AddDays(-30);
        return (f, t);
    }
}
