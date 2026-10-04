using System.ComponentModel.DataAnnotations;

namespace SASMS.Web.Services.Dtos;

public record MonthlyAttendanceRow(
    int EmployeeId,
    string EmployeeName,
    int DaysPresent,
    int DaysLate,
    int DaysAbsent,
    int DaysOnLeave,
    double TotalHoursWorked);

public record LateAttendanceRow(
    int AttendanceId,
    string EmployeeName,
    DateOnly AttendanceDate,
    DateTime? CheckInTime,
    int MinutesLate);

/// <summary>CSV-export-only projection of LateAttendanceRow — the same data, but with
/// CheckInTime pre-converted from stored UTC to Malaysia local time for the exported file,
/// and a clearer column header. LateAttendanceRow itself (used by the interactive Late
/// report view) stays UTC, since ReportService is the business/data layer, not a display
/// concern — the conversion belongs at the export edge, in ReportsController.LateExport.</summary>
public record LateAttendanceExportRow(
    int AttendanceId,
    string EmployeeName,
    DateOnly AttendanceDate,
    [property: Display(Name = "Check In (Malaysia Time)")] DateTime? CheckIn,
    int MinutesLate);

public record AbsenceRow(
    string EmployeeName,
    DateOnly AttendanceDate);

public record OvertimeRow(
    string EmployeeName,
    DateOnly AttendanceDate,
    double HoursWorked,
    double OvertimeHours);

/// <summary>Per-attendance-record worked/overtime hours — the one shared source-of-truth
/// projection (Verified + both check-in/out times present) that both GetOvertimeAsync and
/// the Manager Dashboard's Work Hours Overview are built from, so their numbers can't
/// diverge.</summary>
public record WorkHoursRow(
    int EmployeeId,
    string EmployeeName,
    DateOnly AttendanceDate,
    double HoursWorked,
    double OvertimeHours);
