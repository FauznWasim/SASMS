namespace SASMS.Web.Services.Dtos;

public record AttemptStatus(int CheckInAttemptsUsed, int CheckOutAttemptsUsed, bool CheckInBlocked, bool CheckOutBlocked);
