using System.Globalization;

namespace MentalDesk.Tui.Tests;

public class TimesTests
{
    private readonly FakeClock _clock = new();
    private readonly Times _times;

    public TimesTests() => _times = new Times(_clock);

    private DateTimeOffset Now => _clock.GetUtcNow();

    [Theory]
    [InlineData(0, "now")]
    [InlineData(59, "now")]
    [InlineData(60, "1m ago")]
    [InlineData(89 * 60, "89m ago")]
    [InlineData(90 * 60, "2h ago")]
    [InlineData(35 * 3600, "35h ago")]
    [InlineData(36 * 3600, "2d ago")]
    [InlineData(6 * 86400 + 23 * 3600, "7d ago")]
    [InlineData(7 * 86400, "10 Oct")]
    [InlineData(-30, "now")]
    public void Ago_is_relative_under_a_week_then_the_date(int secondsAgo, string expected) =>
        Assert.Equal(expected, _times.Ago(Now.AddSeconds(-secondsAgo)));

    [Theory]
    [InlineData(59, "now")]
    [InlineData(60, "1m")]
    [InlineData(90 * 60, "2h")]
    [InlineData(36 * 3600, "2d")]
    [InlineData(7 * 86400, "10 Oct")]
    public void Age_drops_ago_for_a_column(int secondsAgo, string expected) =>
        Assert.Equal(expected, _times.Age(Now.AddSeconds(-secondsAgo)));

    [Fact]
    public void A_date_outside_this_year_has_its_year()
    {
        Assert.Equal("9 Nov 2025", _times.Ago(new DateTimeOffset(2025, 11, 9, 12, 0, 0, TimeSpan.Zero)));
        Assert.Equal("9 Nov 2025", _times.Age(new DateTimeOffset(2025, 11, 9, 12, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void At_is_the_time_today_the_date_this_year_and_the_year_otherwise()
    {
        Assert.Equal("09:30", _times.At(new DateTimeOffset(2026, 10, 17, 9, 30, 0, TimeSpan.Zero)));
        Assert.Equal("4 Oct 17:02", _times.At(new DateTimeOffset(2026, 10, 4, 17, 2, 0, TimeSpan.Zero)));
        Assert.Equal("14 Nov 2025 09:30", _times.At(new DateTimeOffset(2025, 11, 14, 9, 30, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void At_shows_a_UTC_time_in_the_local_zone()
    {
        _clock.Zone = TimeZoneInfo.CreateCustomTimeZone("Plus 13", TimeSpan.FromHours(13), "Plus 13", "Plus 13");

        Assert.Equal("03:02", _times.At(new DateTimeOffset(2026, 10, 17, 14, 2, 0, TimeSpan.Zero)));
        Assert.Equal("17 Oct 22:00", _times.At(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Month_names_are_English_whatever_the_culture()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("3 Mar 09:05", _times.At(new DateTimeOffset(2026, 3, 3, 9, 5, 0, TimeSpan.Zero)));
            Assert.Equal("3 Mar", _times.Age(new DateTimeOffset(2026, 3, 3, 9, 5, 0, TimeSpan.Zero)));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Theory]
    [InlineData(45, "45s")]
    [InlineData(300, "5m")]
    [InlineData(2 * 3600, "2h")]
    [InlineData(2 * 3600 + 30, "2h")]
    [InlineData(4 * 3600 + 5 * 60 + 9, "4h 5m")]
    [InlineData(2 * 86400 + 4 * 3600 + 5 * 60, "2d 4h")]
    [InlineData(0, "0s")]
    public void Took_is_the_two_largest_units(int seconds, string expected) =>
        Assert.Equal(expected, Times.Took(TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData(62, "1:02")]
    [InlineData(5, "0:05")]
    [InlineData(3723, "1:02:03")]
    [InlineData(26 * 3600 + 63, "26:01:03")]
    public void Running_is_a_clock(int seconds, string expected) =>
        Assert.Equal(expected, Times.Running(TimeSpan.FromSeconds(seconds)));
}
