using System.Globalization;

namespace MentalDesk.Tui;

// The guide's Times and durations, read against one clock so a test can fix now.
public sealed class Times(TimeProvider? clock = null)
{
    private static readonly CultureInfo English = CultureInfo.InvariantCulture;

    public TimeProvider Clock { get; } = clock ?? TimeProvider.System;

    public string Ago(DateTimeOffset at) => Relative(at) is { } age ? age == "now" ? age : $"{age} ago" : Date(at);

    // For a column whose header says what the age measures.
    public string Age(DateTimeOffset at) => Relative(at) ?? Date(at);

    public string At(DateTimeOffset at)
    {
        var local = Local(at);
        var today = Local(Clock.GetUtcNow());
        if (local.Date == today.Date) return local.ToString("HH:mm", English);
        return local.ToString(local.Year == today.Year ? "d MMM HH:mm" : "d MMM yyyy HH:mm", English);
    }

    public static string Took(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        (int Amount, string Unit)[] units =
            [(span.Days, "d"), (span.Hours, "h"), (span.Minutes, "m"), (span.Seconds, "s")];
        var first = Array.FindIndex(units, unit => unit.Amount > 0);
        if (first < 0) return "0s";
        var largest = $"{units[first].Amount}{units[first].Unit}";
        return first + 1 < units.Length && units[first + 1].Amount > 0
            ? $"{largest} {units[first + 1].Amount}{units[first + 1].Unit}"
            : largest;
    }

    // For a length still counting up or down.
    public static string Running(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        var hours = (int)span.TotalHours;
        return hours > 0
            ? $"{hours}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes}:{span.Seconds:00}";
    }

    // Rounded as git log --date=relative rounds; null from 7 days, which reads as a date.
    private string? Relative(DateTimeOffset at)
    {
        var seconds = (long)Math.Floor((Clock.GetUtcNow() - at).TotalSeconds);
        if (seconds < 60) return "now";
        var minutes = (seconds + 30) / 60;
        if (minutes < 90) return $"{minutes}m";
        var hours = (minutes + 30) / 60;
        if (hours < 36) return $"{hours}h";
        if (seconds >= TimeSpan.FromDays(7).TotalSeconds) return null;
        return $"{(hours + 12) / 24}d";
    }

    private string Date(DateTimeOffset at)
    {
        var local = Local(at);
        return local.ToString(local.Year == Local(Clock.GetUtcNow()).Year ? "d MMM" : "d MMM yyyy", English);
    }

    private DateTimeOffset Local(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, Clock.LocalTimeZone);
}
