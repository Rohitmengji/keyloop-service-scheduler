using System.Globalization;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.UnitTests.Dealerships;

public sealed class OpeningHoursTests
{
    [Theory]
    [InlineData(8, 0, true)]    // starts at opening time
    [InlineData(17, 0, true)]   // finishes exactly at closing time
    [InlineData(7, 45, false)]  // starts before opening
    [InlineData(17, 15, false)] // runs past closing
    public void A_service_must_start_and_finish_within_opening_hours(int hour, int minute, bool open)
    {
        var range = TimeRange.Create(Fixtures.Utc(2026, 11, 2, hour, minute), TimeSpan.FromHours(1));

        Assert.Equal(open, Fixtures.London().IsOpenThroughout(range));
    }

    [Fact]
    public void Opening_hours_are_local_wall_clock_times_that_follow_daylight_saving()
    {
        var london = Fixtures.London();

        // 07:00 UTC is 08:00 BST on Monday 30 March 2026 but 07:00 GMT on Monday 2 November 2026.
        Assert.True(london.IsOpenThroughout(TimeRange.Create(Fixtures.Utc(2026, 3, 30, 7), TimeSpan.FromHours(1))));
        Assert.False(london.IsOpenThroughout(TimeRange.Create(Fixtures.Utc(2026, 11, 2, 7), TimeSpan.FromHours(1))));
    }

    [Fact]
    public void Half_hour_utc_offsets_are_honoured()
    {
        var pune = Fixtures.Pune();

        // Pune opens at 09:00 IST, which is 03:30 UTC.
        Assert.True(pune.IsOpenThroughout(TimeRange.Create(Fixtures.Utc(2026, 11, 2, 3, 30), TimeSpan.FromHours(1))));
        Assert.False(pune.IsOpenThroughout(TimeRange.Create(Fixtures.Utc(2026, 11, 2, 3, 15), TimeSpan.FromHours(1))));
    }

    [Theory]
    [InlineData("2026-11-02")] // an ordinary day
    [InlineData("2026-03-29")] // UK clocks go forward
    [InlineData("2026-10-25")] // UK clocks go back
    public void Candidate_starts_cover_the_local_business_day_even_when_clocks_change(string date)
    {
        var london = Fixtures.London();

        var ranges = london.CandidateRanges(DateOnly.Parse(date, CultureInfo.InvariantCulture), TimeSpan.FromHours(1)).ToList();

        Assert.Equal(37, ranges.Count); // 08:00 to 17:00 every 15 minutes
        Assert.Equal(new TimeOnly(8, 0), LocalTime(london.ToLocalTime(ranges[0].StartUtc)));
        Assert.Equal(new TimeOnly(17, 0), LocalTime(london.ToLocalTime(ranges[^1].StartUtc)));
        Assert.All(ranges, r => Assert.Equal(DateOnly.Parse(date, CultureInfo.InvariantCulture), london.LocalDateOf(r.StartUtc)));
    }

    [Fact]
    public void Candidate_starts_use_the_dealerships_own_time_zone()
    {
        var pune = Fixtures.Pune();

        var ranges = pune.CandidateRanges(new DateOnly(2026, 11, 2), TimeSpan.FromMinutes(30)).ToList();

        Assert.Equal(Fixtures.Utc(2026, 11, 2, 3, 30), ranges[0].StartUtc); // 09:00 IST
        Assert.Equal(Fixtures.Utc(2026, 11, 2, 12), ranges[^1].StartUtc);   // 17:30 IST
    }

    private static TimeOnly LocalTime(DateTimeOffset local) => TimeOnly.FromDateTime(local.DateTime);
}
