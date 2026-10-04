namespace SASMS.Web.Security;

public class BusinessClock : IBusinessClock
{
    // Malaysia is a fixed UTC+8 offset with no daylight saving, but the timezone ID string
    // differs by OS: Windows ships "Singapore Standard Time" (same UTC+8/no-DST rules as
    // Malaysia), while Linux/macOS use the IANA "Asia/Kuala_Lumpur" ID — tried first since
    // it's the portable, deployment-target-agnostic id (also resolvable on modern
    // ICU-backed Windows). Resolved once and cached, since this class is registered as a
    // singleton.
    private static readonly TimeZoneInfo MalaysiaTimeZone = ResolveMalaysiaTimeZone();

    public DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, MalaysiaTimeZone));

    public DateTime ConvertBusinessLocalToUtc(DateOnly date, TimeOnly time)
    {
        var wallClock = date.ToDateTime(time, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(wallClock, MalaysiaTimeZone);
    }

    public DateTime ToMalaysiaTime(DateTime utcInstant) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc), MalaysiaTimeZone);

    private static TimeZoneInfo ResolveMalaysiaTimeZone()
    {
        foreach (var id in new[] { "Asia/Kuala_Lumpur", "Singapore Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        // Last-resort fallback if neither named zone is registered on this machine (e.g. a
        // minimal container image missing tzdata) — Malaysia's offset never changes, so a
        // fixed-offset zone is still a correct answer, not a hack, just less self-documenting
        // than a named IANA/Windows zone.
        return TimeZoneInfo.CreateCustomTimeZone(
            "Malaysia-Fixed-UTC+8", TimeSpan.FromHours(8), "Malaysia Time (UTC+8)", "Malaysia Time (UTC+8)");
    }
}
