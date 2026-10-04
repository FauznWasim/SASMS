namespace SASMS.Web.Security;

/// <summary>Controllers and services must use this (never a client-supplied id) when scoping
/// an Employee's own data, so a request cannot be tampered with to read or modify another
/// employee's records.</summary>
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    int UserId { get; }
    int? EmployeeId { get; }
    string Username { get; }
    bool IsManager { get; }
    string? IpAddress { get; }
}
