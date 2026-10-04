using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SASMS.Web.Models.Entities;
using SASMS.Web.Models.Enums;
using SASMS.Web.Security;

namespace SASMS.Web.Data;

/// <summary>Seeds the two fixed roles and, on first run only, one Manager/PIC account so the system is reachable.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, IPasswordHasher<User> passwordHasher, ILogger logger)
    {
        await db.Database.MigrateAsync();

        if (!await db.Roles.AnyAsync(r => r.RoleId == (int)RoleType.Employee))
        {
            db.Roles.Add(new Role { RoleId = (int)RoleType.Employee, RoleName = RoleNames.Employee });
        }

        if (!await db.Roles.AnyAsync(r => r.RoleId == (int)RoleType.ManagerPic))
        {
            db.Roles.Add(new Role { RoleId = (int)RoleType.ManagerPic, RoleName = RoleNames.Manager });
        }

        await db.SaveChangesAsync();

        var hasManager = await db.Users.AnyAsync(u => u.RoleId == (int)RoleType.ManagerPic);
        if (hasManager)
        {
            return;
        }

        var adminEmployee = new Employee
        {
            FullName = "System Administrator",
            Email = "admin@sasms.local",
            Position = "Administrator",
            Department = "Management",
            EmploymentStatus = EmploymentStatus.Active,
            DateJoined = DateTime.UtcNow
        };
        db.Employees.Add(adminEmployee);
        await db.SaveChangesAsync();

        var temporaryPassword = TemporaryPasswordGenerator.Generate();

        var adminUser = new User
        {
            Username = "admin",
            RoleId = (int)RoleType.ManagerPic,
            EmployeeId = adminEmployee.EmployeeId,
            Status = UserStatus.Active
        };
        adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, temporaryPassword);
        db.Users.Add(adminUser);
        await db.SaveChangesAsync();

        logger.LogWarning(
            "==================================================================\n" +
            "SASMS first-run setup: created default Manager/PIC account.\n" +
            "  Username: admin\n" +
            "  Temporary password: {TemporaryPassword}\n" +
            "This password is shown only once and is NOT stored anywhere in plain text.\n" +
            "Log in and change it immediately via Account > Change Password.\n" +
            "==================================================================",
            temporaryPassword);
    }
}
