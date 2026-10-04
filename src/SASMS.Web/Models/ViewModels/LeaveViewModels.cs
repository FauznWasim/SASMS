using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using SASMS.Web.Models.Enums;

namespace SASMS.Web.Models.ViewModels;

public class LeaveApplyViewModel
{
    [Required(ErrorMessage = "Please select a leave type.")]
    [Display(Name = "Leave Type")]
    public LeaveType? LeaveType { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Date From")]
    public DateOnly StartDate { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Date To")]
    public DateOnly EndDate { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [Display(Name = "Supporting Document")]
    public IFormFile? SupportingDocument { get; set; }
}
