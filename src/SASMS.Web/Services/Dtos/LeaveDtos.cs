using Microsoft.AspNetCore.Http;
using SASMS.Web.Models.Enums;

namespace SASMS.Web.Services.Dtos;

public record SubmitLeaveApplicationInput(
    int EmployeeId,
    DateOnly StartDate,
    DateOnly EndDate,
    string Reason,
    LeaveType LeaveType,
    IFormFile? SupportingDocument);
