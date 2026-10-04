using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace SASMS.Web.Services;

/// <summary>
/// A unique-index violation is the real backstop against races an app-level "does this
/// already exist" check can't close on its own (two concurrent requests can both pass the
/// check before either commits). Services catch this instead of letting it surface as a
/// raw 500, since a 500 in a request that would just re-check as invalid on a plain retry
/// is worse UX than a normal validation-style failure.
/// </summary>
public static class DbConcurrencyHelper
{
    private const int MySqlDuplicateEntryErrorNumber = 1062;

    public static bool IsDuplicateKeyViolation(DbUpdateException ex) =>
        ex.InnerException is MySqlException { Number: MySqlDuplicateEntryErrorNumber };
}
