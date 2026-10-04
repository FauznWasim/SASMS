using System.ComponentModel.DataAnnotations;
using SASMS.Web.Models.Entities;
using SASMS.Web.Models.Enums;
using SASMS.Web.Services.Dtos;

namespace SASMS.Web.Models.ViewModels;

public class EmployeeDetailsViewModel
{
    public Employee Employee { get; set; } = null!;
    public AttemptStatus AttemptStatus { get; set; } = new(0, 0, false, false);
    public bool HasTodayRecord { get; set; }
}

public class EmployeeCreateViewModel
{
    [Required, MaxLength(200)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [MaxLength(20)]
    [RegularExpression(@"^0\d{1,2}-?\d{6,8}$", ErrorMessage = "Enter a valid Malaysian phone number, e.g. 012-3456789 or 03-12345678.")]
    public string Phone { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Position { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Department { get; set; }

    [Required, MaxLength(100)]
    [Display(Name = "Login username")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Account role")]
    public RoleType Role { get; set; } = RoleType.Employee;
}

public class EmployeeEditViewModel
{
    public int EmployeeId { get; set; }

    [Required, MaxLength(200)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [MaxLength(20)]
    [RegularExpression(@"^0\d{1,2}-?\d{6,8}$", ErrorMessage = "Enter a valid Malaysian phone number, e.g. 012-3456789 or 03-12345678.")]
    public string Phone { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Position { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Department { get; set; }
}
