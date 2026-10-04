namespace SASMS.Web.Models.ViewModels;

/// <summary>Manager Dashboard's "Work Hours Overview" card — built entirely from
/// IReportService.GetWorkHoursAsync, the same source-of-truth the Reports page uses, so
/// these numbers can never disagree with the Overtime/Monthly reports.</summary>
public class WorkHoursOverviewViewModel
{
    public string Period { get; set; } = "today"; // "today" | "week" | "month"
    public double TotalWorkedHours { get; set; }
    public double TotalOvertimeHours { get; set; }
    public int EmployeesTracked { get; set; }
    public List<WorkHoursChartPoint> ChartPoints { get; set; } = new();
}

/// <summary>One bar in the Work Hours Overview chart — an employee (Today), a day (This
/// Week), or a week (This Month), depending on the selected period.</summary>
public record WorkHoursChartPoint(string Label, double TotalHours, double OvertimeHours);
