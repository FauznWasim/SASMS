using System.ComponentModel.DataAnnotations;

namespace SASMS.Web.Models.ApiDtos;

/// <summary>Payload posted to Api/VerificationController by an external/decoupled
/// verification caller — not used by the interactive check-in/out flow.</summary>
public class VerificationCallbackDto
{
    [Required]
    public int EmployeeId { get; set; }

    [Required]
    public DateOnly AttendanceDate { get; set; }

    [Required]
    public bool FaceMatchSuccess { get; set; }

    [Required]
    public bool LivenessPassed { get; set; }
}
