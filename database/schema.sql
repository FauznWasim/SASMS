-- SASMS backend-phase schema.
-- Manual fallback for creating the database directly via phpMyAdmin/XAMPP.
-- This mirrors src/SASMS.Web/Data/ApplicationDbContext.cs — EF Core migrations
-- (`dotnet ef database update`) are the source of truth; run this only if you
-- want to inspect/create the schema without the .NET SDK/tooling.

CREATE DATABASE IF NOT EXISTS sasms_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE sasms_db;

CREATE TABLE IF NOT EXISTS Roles (
    RoleId INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    RoleName VARCHAR(50) NOT NULL,
    UNIQUE KEY UX_Roles_RoleName (RoleName)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Employees (
    EmployeeId INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    FullName VARCHAR(200) NOT NULL,
    Email VARCHAR(200) NOT NULL,
    Phone VARCHAR(50) NULL,
    Position VARCHAR(100) NOT NULL,
    Department VARCHAR(100) NULL,
    EmploymentStatus VARCHAR(20) NOT NULL DEFAULT 'Active',
    DateJoined DATETIME NOT NULL,
    FaceTemplateRef VARCHAR(500) NULL,
    UNIQUE KEY UX_Employees_Email (Email)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Users (
    UserId INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    Username VARCHAR(100) NOT NULL,
    PasswordHash VARCHAR(255) NOT NULL,
    RoleId INT NOT NULL,
    EmployeeId INT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Active',
    FailedLoginCount INT NOT NULL DEFAULT 0,
    LockoutEndUtc DATETIME NULL,
    CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY UX_Users_Username (Username),
    UNIQUE KEY UX_Users_EmployeeId (EmployeeId),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES Roles (RoleId) ON DELETE RESTRICT,
    CONSTRAINT FK_Users_Employees FOREIGN KEY (EmployeeId) REFERENCES Employees (EmployeeId) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Attendances (
    AttendanceId INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    EmployeeId INT NOT NULL,
    AttendanceDate DATE NOT NULL,
    CheckInTime DATETIME NULL,
    CheckOutTime DATETIME NULL,
    AttendanceStatus VARCHAR(20) NOT NULL DEFAULT 'Present',
    VerificationStatus VARCHAR(20) NOT NULL DEFAULT 'Pending',
    CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY UX_Attendances_Employee_Date (EmployeeId, AttendanceDate),
    CONSTRAINT FK_Attendances_Employees FOREIGN KEY (EmployeeId) REFERENCES Employees (EmployeeId) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Schedules (
    ScheduleId INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    EmployeeId INT NOT NULL,
    ShiftDate DATE NOT NULL,
    StartTime TIME NOT NULL,
    EndTime TIME NOT NULL,
    Notes VARCHAR(500) NULL,
    CreatedByUserId INT NOT NULL,
    CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Schedules_Employees FOREIGN KEY (EmployeeId) REFERENCES Employees (EmployeeId) ON DELETE RESTRICT,
    CONSTRAINT FK_Schedules_Users FOREIGN KEY (CreatedByUserId) REFERENCES Users (UserId) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Notifications (
    NotificationId INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    UserId INT NOT NULL,
    Title VARCHAR(200) NOT NULL,
    Message VARCHAR(1000) NOT NULL,
    IsRead TINYINT(1) NOT NULL DEFAULT 0,
    CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS AuditLogs (
    AuditLogId INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    UserId INT NULL,
    Action VARCHAR(100) NOT NULL,
    EntityName VARCHAR(100) NOT NULL,
    EntityId VARCHAR(100) NULL,
    Details VARCHAR(1000) NULL,
    IpAddress VARCHAR(64) NULL,
    TimestampUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_AuditLogs_Users FOREIGN KEY (UserId) REFERENCES Users (UserId) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT INTO Roles (RoleId, RoleName) VALUES (1, 'Employee'), (2, 'Manager/PIC')
    ON DUPLICATE KEY UPDATE RoleName = VALUES(RoleName);

-- No default Manager/PIC user is inserted here: the application's DbSeeder creates one
-- on first run with a randomly generated password (never a hardcoded default credential).
