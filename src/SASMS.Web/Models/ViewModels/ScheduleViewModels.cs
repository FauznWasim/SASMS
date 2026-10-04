using System.ComponentModel.DataAnnotations;

namespace SASMS.Web.Models.ViewModels;

public class ScheduleCreateViewModel
{
    [Required]
    [Display(Name = "Employee")]
    public int EmployeeId { get; set; }

    // No default here — ViewModels are plain POCOs with no DI access; SchedulesController.Create
    // (GET) sets this explicitly via IBusinessClock instead of DateTime.Today (server-local,
    // not guaranteed to be Malaysia time on a real deployment host).
    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Shift date")]
    public DateOnly ShiftDate { get; set; }

    [Required]
    [DataType(DataType.Time)]
    [Display(Name = "Start time")]
    public TimeOnly StartTime { get; set; }

    [Required]
    [DataType(DataType.Time)]
    [Display(Name = "End time")]
    public TimeOnly EndTime { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class ScheduleEditViewModel
{
    public int ScheduleId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Shift date")]
    public DateOnly ShiftDate { get; set; }

    [Required]
    [DataType(DataType.Time)]
    [Display(Name = "Start time")]
    public TimeOnly StartTime { get; set; }

    [Required]
    [DataType(DataType.Time)]
    [Display(Name = "End time")]
    public TimeOnly EndTime { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
