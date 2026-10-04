using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace SASMS.Web.Services;

/// <summary>Reflection-based CSV writer for simple record/DTO lists used by report exports.</summary>
public static class CsvExportHelper
{
    public static byte[] ToCsv<T>(IEnumerable<T> rows)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var sb = new StringBuilder();

        sb.AppendLine(string.Join(',', properties.Select(p => Escape(HeaderName(p)))));

        foreach (var row in rows)
        {
            var values = properties.Select(p => Escape(FormatValue(p.GetValue(row))));
            sb.AppendLine(string.Join(',', values));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    // Optional per-property header override via [Display(Name = "...")] — lets a DTO give a
    // clearer CSV column name (e.g. "Check In (Malaysia Time)") without changing every other
    // export's plain property-name headers, since only properties that opt in are affected.
    private static string HeaderName(PropertyInfo p) =>
        p.GetCustomAttribute<DisplayAttribute>()?.Name ?? p.Name;

    private static string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        double d => d.ToString("0.##", CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
