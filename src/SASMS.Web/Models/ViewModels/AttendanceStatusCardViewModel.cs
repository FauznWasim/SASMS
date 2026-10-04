using SASMS.Web.Models.Entities;

namespace SASMS.Web.Models.ViewModels;

/// <summary>Shared by Employee and Manager/PIC dashboards — both roles check in/out for
/// themselves and need the same "today's attendance + face registration" card.</summary>
public class AttendanceStatusCardViewModel
{
    public Attendance? TodayRecord { get; set; }
    public bool FaceRegistered { get; set; }

    /// <summary>Whether a Schedule row exists for this employee today — the Schedule table is the
    /// sole authority for "expected to work"; a rest day never shows an active Check In button.</summary>
    public bool HasScheduleToday { get; set; }
}
