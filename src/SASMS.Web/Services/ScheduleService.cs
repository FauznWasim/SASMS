using Microsoft.EntityFrameworkCore;
using SASMS.Web.Data;
using SASMS.Web.Models.Entities;
using SASMS.Web.Services.Dtos;

namespace SASMS.Web.Services;

public class ScheduleService : IScheduleService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;

    public ScheduleService(ApplicationDbContext db, IAuditService auditService, INotificationService notificationService)
    {
        _db = db;
        _auditService = auditService;
        _notificationService = notificationService;
    }

    public async Task<List<Schedule>> GetAllAsync(DateOnly? from, DateOnly? to)
    {
        var query = _db.Schedules.Include(s => s.Employee).AsQueryable();
        if (from is not null) query = query.Where(s => s.ShiftDate >= from);
        if (to is not null) query = query.Where(s => s.ShiftDate <= to);
        return await query.OrderBy(s => s.ShiftDate).ThenBy(s => s.StartTime).ToListAsync();
    }

    public async Task<List<Schedule>> GetByEmployeeAsync(int employeeId, DateOnly? from, DateOnly? to)
    {
        var query = _db.Schedules.Where(s => s.EmployeeId == employeeId);
        if (from is not null) query = query.Where(s => s.ShiftDate >= from);
        if (to is not null) query = query.Where(s => s.ShiftDate <= to);
        return await query.OrderBy(s => s.ShiftDate).ThenBy(s => s.StartTime).ToListAsync();
    }

    public async Task<Schedule?> GetByIdAsync(int scheduleId)
    {
        return await _db.Schedules.Include(s => s.Employee).FirstOrDefaultAsync(s => s.ScheduleId == scheduleId);
    }

    public async Task<ServiceResult<int>> CreateAsync(CreateScheduleInput input, int actingUserId, string? ipAddress)
    {
        if (input.EndTime <= input.StartTime)
        {
            return ServiceResult<int>.Fail("Shift end time must be after the start time.");
        }

        var employee = await _db.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.EmployeeId == input.EmployeeId);
        if (employee is null)
        {
            return ServiceResult<int>.Fail("Employee not found.");
        }

        var overlap = await _db.Schedules.AnyAsync(s => s.EmployeeId == input.EmployeeId && s.ShiftDate == input.ShiftDate);
        if (overlap)
        {
            return ServiceResult<int>.Fail("This employee already has a shift scheduled on that date.");
        }

        var schedule = new Schedule
        {
            EmployeeId = input.EmployeeId,
            ShiftDate = input.ShiftDate,
            StartTime = input.StartTime,
            EndTime = input.EndTime,
            Notes = input.Notes,
            CreatedByUserId = actingUserId,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Schedules.Add(schedule);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (DbConcurrencyHelper.IsDuplicateKeyViolation(ex))
        {
            return ServiceResult<int>.Fail("This employee already has a shift scheduled on that date.");
        }

        if (employee.User is not null)
        {
            await _notificationService.NotifyAsync(employee.User.UserId, "New Shift Scheduled",
                $"A shift was scheduled for you on {input.ShiftDate:yyyy-MM-dd} ({input.StartTime:HH\\:mm}-{input.EndTime:HH\\:mm}).");
        }

        await _auditService.LogAsync(actingUserId, "Create", "Schedule", schedule.ScheduleId.ToString(),
            $"Scheduled employee {input.EmployeeId} for {input.ShiftDate:yyyy-MM-dd}.", ipAddress);

        return ServiceResult<int>.Ok(schedule.ScheduleId);
    }

    public async Task<ServiceResult> UpdateAsync(int scheduleId, UpdateScheduleInput input, int actingUserId, string? ipAddress)
    {
        if (input.EndTime <= input.StartTime)
        {
            return ServiceResult.Fail("Shift end time must be after the start time.");
        }

        var schedule = await _db.Schedules.Include(s => s.Employee).ThenInclude(e => e.User)
            .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId);
        if (schedule is null)
        {
            return ServiceResult.Fail("Schedule not found.");
        }

        schedule.ShiftDate = input.ShiftDate;
        schedule.StartTime = input.StartTime;
        schedule.EndTime = input.EndTime;
        schedule.Notes = input.Notes;
        await _db.SaveChangesAsync();

        if (schedule.Employee.User is not null)
        {
            await _notificationService.NotifyAsync(schedule.Employee.User.UserId, "Shift Updated",
                $"Your shift on {input.ShiftDate:yyyy-MM-dd} was updated to {input.StartTime:HH\\:mm}-{input.EndTime:HH\\:mm}.");
        }

        await _auditService.LogAsync(actingUserId, "Update", "Schedule", scheduleId.ToString(),
            $"Updated schedule for employee {schedule.EmployeeId}.", ipAddress);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(int scheduleId, int actingUserId, string? ipAddress)
    {
        var schedule = await _db.Schedules.Include(s => s.Employee).ThenInclude(e => e.User)
            .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId);
        if (schedule is null)
        {
            return ServiceResult.Fail("Schedule not found.");
        }

        _db.Schedules.Remove(schedule);
        await _db.SaveChangesAsync();

        if (schedule.Employee.User is not null)
        {
            await _notificationService.NotifyAsync(schedule.Employee.User.UserId, "Shift Cancelled",
                $"Your shift on {schedule.ShiftDate:yyyy-MM-dd} was cancelled.");
        }

        await _auditService.LogAsync(actingUserId, "Delete", "Schedule", scheduleId.ToString(),
            $"Deleted schedule for employee {schedule.EmployeeId}.", ipAddress);

        return ServiceResult.Ok();
    }
}
