using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Domain.Dealerships;

public sealed class Dealership
{
    private Dealership()
    {
    }

    public Dealership(Guid id, string name, string timeZoneId, TimeOnly opensAt, TimeOnly closesAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        if (opensAt >= closesAt)
        {
            throw new ArgumentException("Opening time must be before closing time.", nameof(closesAt));
        }

        _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        Id = id;
        Name = name;
        TimeZoneId = timeZoneId;
        OpensAt = opensAt;
        ClosesAt = closesAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>IANA time zone (for example <c>Europe/London</c>); opening hours are wall-clock times in this zone.</summary>
    public string TimeZoneId { get; private set; } = string.Empty;

    public TimeOnly OpensAt { get; private set; }

    public TimeOnly ClosesAt { get; private set; }

    public DateTimeOffset ToLocalTime(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId));

    public DateOnly LocalDateOf(DateTimeOffset instant) => DateOnly.FromDateTime(ToLocalTime(instant).DateTime);

    /// <summary>True when the whole range sits inside the opening hours of a single local business day.</summary>
    public bool IsOpenThroughout(TimeRange range)
    {
        var localStart = ToLocalTime(range.StartUtc);
        var localEnd = ToLocalTime(range.EndUtc);
        return localStart.Date == localEnd.Date
            && TimeOnly.FromTimeSpan(localStart.TimeOfDay) >= OpensAt
            && TimeOnly.FromTimeSpan(localEnd.TimeOfDay) <= ClosesAt;
    }

    /// <summary>Every slot-aligned range of the given duration that starts on <paramref name="localDate"/> and fits opening hours.</summary>
    public IEnumerable<TimeRange> CandidateRanges(DateOnly localDate, TimeSpan duration)
    {
        // Scan every UTC slot that could fall on this local date (offsets span -12h..+14h) instead of converting
        // local clock times to UTC, which keeps DST transition days correct without special cases.
        var midnightUtc = new DateTimeOffset(localDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        for (var start = midnightUtc.AddHours(-14); start < midnightUtc.AddHours(36); start += Slots.Length)
        {
            var range = TimeRange.Create(start, duration);
            if (LocalDateOf(start) == localDate && IsOpenThroughout(range))
            {
                yield return range;
            }
        }
    }
}
