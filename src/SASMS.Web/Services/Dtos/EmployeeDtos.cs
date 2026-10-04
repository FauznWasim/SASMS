using SASMS.Web.Models.Enums;

namespace SASMS.Web.Services.Dtos;

public record CreateEmployeeInput(
    string FullName,
    string Email,
    string? Phone,
    string Position,
    string? Department,
    string Username,
    RoleType Role);

public record UpdateEmployeeInput(
    string FullName,
    string Email,
    string? Phone,
    string Position,
    string? Department);

public record CreateEmployeeResult(int EmployeeId, int UserId, string TemporaryPassword);
