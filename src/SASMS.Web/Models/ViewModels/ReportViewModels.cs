using System.ComponentModel.DataAnnotations;

namespace SASMS.Web.Models.ViewModels;

public class ReportFilterViewModel
{
    // No property-level defaults here — ViewModels are plain POCOs with no DI access, so the
    // Malaysia-business-date defaults are set explicitly by ReportsController.Index() via
    // IBusinessClock instead of DateTime.Today (server-local, not guaranteed to be Malaysia
    // time on a real deployment host).
    [Display(Name = "From")]
    [DataType(DataType.Date)]
    public DateOnly From { get; set; }

    [Display(Name = "To")]
    [DataType(DataType.Date)]
    public DateOnly To { get; set; }

    [Range(2000, 2100)]
    public int Year { get; set; }

    [Range(1, 12)]
    public int Month { get; set; }
}
