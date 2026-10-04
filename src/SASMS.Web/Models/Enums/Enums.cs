using System.ComponentModel.DataAnnotations;

namespace SASMS.Web.Models.Enums;

public enum RoleType
{
    Employee = 1,

    [Display(Name = "Manager/PIC")]
    ManagerPic = 2
}

public enum UserStatus
{
    Active,
    Disabled
}

public enum EmploymentStatus
{
    Active,
    Inactive
}

public enum AttendanceStatus
{
    Present,
    Late,
    Absent,
    OnLeave
}

public enum VerificationStatus
{
    Pending,
    Verified,
    Failed,

    /// <summary>No facial verification was ever attempted for this record — e.g. a Manager/PIC
    /// manually marked the employee Absent. Distinct from Verified/Failed/Pending, all of which
    /// imply a real or expected verification event.</summary>
    NotApplicable
}

public enum LeaveStatus
{
    Pending,
    Approved,
    Rejected
}

public enum LeaveType
{
    Annual,
    Medical,
    Emergency,
    Unpaid,
    Other
}
