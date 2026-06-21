// Codex developer note: Parses tenant refresh-period strings shared by env loading, DB seed, and API validation.
using System.Globalization;
using System.Text.RegularExpressions;

namespace webapi_oyako.Infrastructure.Configuration;

public static partial class TenantRefreshPeriodParser
{
    public static bool TryParseMinutes(string? value, out int minutes)
    {
        minutes = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = RefreshPeriodRegex().Match(value.Trim());
        if (!match.Success || !int.TryParse(match.Groups["count"].Value, CultureInfo.InvariantCulture, out var count))
        {
            return false;
        }

        var unit = match.Groups["unit"].Value.ToLowerInvariant();
        minutes = unit switch
        {
            "minute" or "minutes" => count,
            "hour" or "hours" => count * 60,
            "day" or "days" => count * 24 * 60,
            "week" or "weeks" => count * 7 * 24 * 60,
            _ => 0
        };

        return unit switch
        {
            "minute" or "minutes" => count is >= 1 and <= 60,
            "hour" or "hours" => count is >= 1 and <= 24,
            "day" or "days" => count is >= 1 and <= 4,
            "week" or "weeks" => count is >= 1 and <= 4,
            _ => false
        };
    }

    public static (int Value, string Unit) ToValueUnit(int minutes)
    {
        if (minutes % (7 * 24 * 60) == 0)
        {
            var weeks = minutes / (7 * 24 * 60);
            if (weeks is >= 1 and <= 4)
            {
                return (weeks, "week");
            }
        }

        if (minutes % (24 * 60) == 0)
        {
            var days = minutes / (24 * 60);
            if (days is >= 1 and <= 4)
            {
                return (days, "day");
            }
        }

        if (minutes % 60 == 0)
        {
            var hours = minutes / 60;
            if (hours is >= 1 and <= 24)
            {
                return (hours, "hour");
            }
        }

        return (Math.Clamp(minutes, 1, 60), "minute");
    }

    public static string Format(int minutes)
    {
        var (value, unit) = ToValueUnit(minutes);
        return FormattableString.Invariant($"{value}{unit}");
    }

    [GeneratedRegex(@"^(?<count>\d+)\s*(?<unit>minute|minutes|hour|hours|day|days|week|weeks)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RefreshPeriodRegex();
}
