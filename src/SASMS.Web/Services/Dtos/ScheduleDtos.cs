namespace SASMS.Web.Services.Dtos;

public record CreateScheduleInput(int EmployeeId, DateOnly ShiftDate, TimeOnly StartTime, TimeOnly EndTime, string? Notes);

public record UpdateScheduleInput(DateOnly ShiftDate, TimeOnly StartTime, TimeOnly EndTime, string? Notes);
