using Microsoft.EntityFrameworkCore;
using SASMS.Web.Models.Entities;

namespace SASMS.Web.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<LeaveApplication> LeaveApplications => Set<LeaveApplication>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(r => r.RoleId);
            entity.Property(r => r.RoleName).IsRequired().HasMaxLength(50);
            entity.HasIndex(r => r.RoleName).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.UserId);
            entity.Property(u => u.Username).IsRequired().HasMaxLength(100);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.Status).HasConversion<string>().HasMaxLength(20);

            entity.HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(u => u.Employee)
                .WithOne(e => e.User)
                .HasForeignKey<User>(u => u.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.EmployeeId);
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Position).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EmploymentStatus).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.HasKey(a => a.AttendanceId);
            entity.Property(a => a.AttendanceStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(a => a.VerificationStatus).HasConversion<string>().HasMaxLength(20);

            entity.HasOne(a => a.Employee)
                .WithMany(e => e.AttendanceRecords)
                .HasForeignKey(a => a.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // An employee can only have one attendance record per calendar date.
            entity.HasIndex(a => new { a.EmployeeId, a.AttendanceDate }).IsUnique();
        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.HasKey(s => s.ScheduleId);

            entity.HasOne(s => s.Employee)
                .WithMany(e => e.Schedules)
                .HasForeignKey(s => s.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.CreatedByUser)
                .WithMany(u => u.CreatedSchedules)
                .HasForeignKey(s => s.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // DB-level backstop against the same race the app-level overlap check can't
            // close on its own: two concurrent requests could both pass the "no existing
            // shift that day" check before either commits.
            entity.HasIndex(s => new { s.EmployeeId, s.ShiftDate }).IsUnique();
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(n => n.NotificationId);
            entity.Property(n => n.Title).IsRequired().HasMaxLength(200);
            entity.Property(n => n.Message).IsRequired().HasMaxLength(1000);

            entity.HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(a => a.AuditLogId);
            entity.Property(a => a.Action).IsRequired().HasMaxLength(100);
            entity.Property(a => a.EntityName).IsRequired().HasMaxLength(100);

            entity.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LeaveApplication>(entity =>
        {
            entity.HasKey(l => l.LeaveApplicationId);
            entity.Property(l => l.Reason).IsRequired().HasMaxLength(1000);
            entity.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(l => l.LeaveType).HasConversion<string>().HasMaxLength(20);
            entity.Property(l => l.AttachmentStoredFileName).HasMaxLength(260);
            entity.Property(l => l.AttachmentOriginalFileName).HasMaxLength(260);

            entity.HasOne(l => l.Employee)
                .WithMany()
                .HasForeignKey(l => l.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.ReviewedByUser)
                .WithMany()
                .HasForeignKey(l => l.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
