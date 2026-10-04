using SASMS.Web.Models.Entities;
using SASMS.Web.Services.Dtos;

namespace SASMS.Web.Services;

public interface IEmployeeService
{
    Task<List<Employee>> GetAllAsync();
    Task<Employee?> GetByIdAsync(int employeeId);
    Task<ServiceResult<CreateEmployeeResult>> CreateAsync(CreateEmployeeInput input, int actingUserId, string? ipAddress);
    Task<ServiceResult> UpdateAsync(int employeeId, UpdateEmployeeInput input, int actingUserId, string? ipAddress);
    Task<ServiceResult> DeactivateAsync(int employeeId, int actingUserId, string? ipAddress);
    Task<ServiceResult> ReactivateAsync(int employeeId, int actingUserId, string? ipAddress);
    Task<ServiceResult> MarkFaceRegisteredAsync(int employeeId, int actingUserId, string? ipAddress);
    Task<ServiceResult<string>> ResetPasswordAsync(int employeeId, int actingUserId, string? ipAddress);

    /// <summary>Manager/PIC-only: cleanly removes an employee's registered face (deletes their
    /// reference photos and has the face-service rebuild its model without them), then clears
    /// Employee.FaceTemplateRef — but only once the face-service deletion is confirmed to have
    /// succeeded, so the database never claims "unregistered" while stale biometric data still
    /// exists. Does not touch attendance, schedule, leave, notifications, audit history, or any
    /// other employee field. Does not delete the employee or their account.</summary>
    Task<ServiceResult> ResetFaceRegistrationAsync(int employeeId, int actingUserId, string? ipAddress);
}
