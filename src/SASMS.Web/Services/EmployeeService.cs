using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SASMS.Web.Data;
using SASMS.Web.Models.Entities;
using SASMS.Web.Models.Enums;
using SASMS.Web.Security;
using SASMS.Web.Services.Dtos;

namespace SASMS.Web.Services;

public class EmployeeService : IEmployeeService
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IAuditService _auditService;
    private readonly IFaceVerificationClient _faceVerificationClient;

    public EmployeeService(
        ApplicationDbContext db,
        IPasswordHasher<User> passwordHasher,
        IAuditService auditService,
        IFaceVerificationClient faceVerificationClient)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _auditService = auditService;
        _faceVerificationClient = faceVerificationClient;
    }

    /// <summary>Normalizes human-readable identity fields (name, position, department) to uppercase
    /// for consistent display/search — deliberately never applied to email, username, phone, or
    /// password, where case is meaningful or user-chosen.</summary>
    private static string? NormalizeToUpper(string? value) =>
        string.IsNullOrWhiteSpace(value) ? value : value.Trim().ToUpperInvariant();

    public async Task<List<Employee>> GetAllAsync()
    {
        return await _db.Employees
            .Include(e => e.User).ThenInclude(u => u!.Role)
            .OrderBy(e => e.FullName)
            .ToListAsync();
    }

    public async Task<Employee?> GetByIdAsync(int employeeId)
    {
        return await _db.Employees
            .Include(e => e.User).ThenInclude(u => u!.Role)
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
    }

    public async Task<ServiceResult<CreateEmployeeResult>> CreateAsync(CreateEmployeeInput input, int actingUserId, string? ipAddress)
    {
        var usernameTaken = await _db.Users.AnyAsync(u => u.Username == input.Username);
        if (usernameTaken)
        {
            return ServiceResult<CreateEmployeeResult>.Fail("That username is already taken.");
        }

        var emailTaken = await _db.Employees.AnyAsync(e => e.Email == input.Email);
        if (emailTaken)
        {
            return ServiceResult<CreateEmployeeResult>.Fail("That email is already registered to an employee.");
        }

        // EnableRetryOnFailure (Program.cs) means a manually-owned transaction must be
        // driven through an execution strategy — otherwise EF Core throws at runtime the
        // first time this actually retries, since the retrying strategy can't safely
        // re-run a transaction it doesn't control the boundaries of.
        var strategy = _db.Database.CreateExecutionStrategy();
        var (employee, user, temporaryPassword) = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            var employee = new Employee
            {
                // Identity/descriptive fields normalized to uppercase for consistent display and
                // search — never applied to email/username/phone/password, where case matters.
                FullName = NormalizeToUpper(input.FullName)!,
                Email = input.Email,
                Phone = input.Phone,
                Position = NormalizeToUpper(input.Position)!,
                Department = NormalizeToUpper(input.Department),
                EmploymentStatus = EmploymentStatus.Active,
                DateJoined = DateTime.UtcNow
            };
            _db.Employees.Add(employee);
            await _db.SaveChangesAsync();

            var temporaryPassword = TemporaryPasswordGenerator.Generate();

            var user = new User
            {
                Username = input.Username,
                RoleId = (int)input.Role,
                EmployeeId = employee.EmployeeId,
                Status = UserStatus.Active
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, temporaryPassword);
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            return (employee, user, temporaryPassword);
        });

        await _auditService.LogAsync(actingUserId, "Create", "Employee", employee.EmployeeId.ToString(),
            $"Created employee '{employee.FullName}' with linked user '{user.Username}'.", ipAddress);

        return ServiceResult<CreateEmployeeResult>.Ok(new CreateEmployeeResult(employee.EmployeeId, user.UserId, temporaryPassword));
    }

    public async Task<ServiceResult> UpdateAsync(int employeeId, UpdateEmployeeInput input, int actingUserId, string? ipAddress)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        if (employee is null)
        {
            return ServiceResult.Fail("Employee not found.");
        }

        var emailTaken = await _db.Employees.AnyAsync(e => e.Email == input.Email && e.EmployeeId != employeeId);
        if (emailTaken)
        {
            return ServiceResult.Fail("That email is already registered to another employee.");
        }

        employee.FullName = NormalizeToUpper(input.FullName)!;
        employee.Email = input.Email;
        employee.Phone = input.Phone;
        employee.Position = NormalizeToUpper(input.Position)!;
        employee.Department = NormalizeToUpper(input.Department);

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(actingUserId, "Update", "Employee", employeeId.ToString(),
            $"Updated employee '{employee.FullName}'.", ipAddress);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeactivateAsync(int employeeId, int actingUserId, string? ipAddress)
    {
        var employee = await _db.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        if (employee is null)
        {
            return ServiceResult.Fail("Employee not found.");
        }

        // Soft delete only: attendance/schedule history must remain intact for auditability (doc section 16).
        employee.EmploymentStatus = EmploymentStatus.Inactive;
        if (employee.User is not null)
        {
            employee.User.Status = UserStatus.Disabled;
        }

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(actingUserId, "Deactivate", "Employee", employeeId.ToString(),
            $"Deactivated employee '{employee.FullName}' and disabled their account.", ipAddress);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ReactivateAsync(int employeeId, int actingUserId, string? ipAddress)
    {
        var employee = await _db.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        if (employee is null)
        {
            return ServiceResult.Fail("Employee not found.");
        }

        employee.EmploymentStatus = EmploymentStatus.Active;
        if (employee.User is not null)
        {
            employee.User.Status = UserStatus.Active;
            employee.User.FailedLoginCount = 0;
            employee.User.LockoutEndUtc = null;
        }

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(actingUserId, "Reactivate", "Employee", employeeId.ToString(),
            $"Reactivated employee '{employee.FullName}' and re-enabled their account.", ipAddress);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<string>> ResetPasswordAsync(int employeeId, int actingUserId, string? ipAddress)
    {
        var employee = await _db.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        if (employee is null)
        {
            return ServiceResult<string>.Fail("Employee not found.");
        }

        if (employee.User is null)
        {
            return ServiceResult<string>.Fail("This employee has no login account.");
        }

        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        employee.User.PasswordHash = _passwordHasher.HashPassword(employee.User, temporaryPassword);
        // A password reset is also a reasonable moment to clear any lockout — the old
        // password (and whatever failed attempts were made against it) no longer matters.
        employee.User.FailedLoginCount = 0;
        employee.User.LockoutEndUtc = null;

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(actingUserId, "ResetPassword", "Employee", employeeId.ToString(),
            $"Reset login password for '{employee.FullName}'.", ipAddress);

        return ServiceResult<string>.Ok(temporaryPassword);
    }

    public async Task<ServiceResult> MarkFaceRegisteredAsync(int employeeId, int actingUserId, string? ipAddress)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        if (employee is null)
        {
            return ServiceResult.Fail("Employee not found.");
        }

        // The actual biometric template lives in face-service's own storage, not this DB —
        // this column is just the "has this employee registered?" marker the app checks.
        employee.FaceTemplateRef = $"registered:{DateTime.UtcNow:O}";
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(actingUserId, "RegisterFace", "Employee", employeeId.ToString(),
            $"Registered a facial verification template for '{employee.FullName}'.", ipAddress);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ResetFaceRegistrationAsync(int employeeId, int actingUserId, string? ipAddress)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        if (employee is null)
        {
            return ServiceResult.Fail("Employee not found.");
        }

        if (employee.FaceTemplateRef is null)
        {
            return ServiceResult.Fail("This employee does not have a registered face to reset.");
        }

        // Only clear the "registered" marker once the face-service confirms the old biometric
        // data was actually removed — never claim "unregistered" while stale data might still
        // exist (e.g. the face-service being unreachable right now).
        var deleted = await _faceVerificationClient.DeleteFaceAsync(employeeId);
        if (!deleted)
        {
            return ServiceResult.Fail("Could not reset face registration — the face verification service is unavailable or the operation failed. No changes were made; please try again.");
        }

        employee.FaceTemplateRef = null;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(actingUserId, "ResetFaceRegistration", "Employee", employeeId.ToString(),
            $"Reset face registration for '{employee.FullName}' — old biometric data removed from the face-service; they must register again before checking in or out.",
            ipAddress);

        return ServiceResult.Ok();
    }
}
