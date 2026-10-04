using SASMS.Web.Models.Entities;
using SASMS.Web.Services.Dtos;

namespace SASMS.Web.Services;

public interface IScheduleService
{
    Task<List<Schedule>> GetAllAsync(DateOnly? from, DateOnly? to);
    Task<List<Schedule>> GetByEmployeeAsync(int employeeId, DateOnly? from, DateOnly? to);
    Task<Schedule?> GetByIdAsync(int scheduleId);
    Task<ServiceResult<int>> CreateAsync(CreateScheduleInput input, int actingUserId, string? ipAddress);
    Task<ServiceResult> UpdateAsync(int scheduleId, UpdateScheduleInput input, int actingUserId, string? ipAddress);
    Task<ServiceResult> DeleteAsync(int scheduleId, int actingUserId, string? ipAddress);
}
