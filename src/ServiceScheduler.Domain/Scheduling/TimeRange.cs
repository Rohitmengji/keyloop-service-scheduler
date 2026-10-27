namespace ServiceScheduler.Domain.Scheduling;

/// <summary>A half-open UTC interval [StartUtc, EndUtc) made of whole slots.</summary>
public readonly record struct TimeRange
{
    private TimeRange(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public DateTimeOffset StartUtc { get; }

    public DateTimeOffset EndUtc { get; }

    public static TimeRange Create(DateTimeOffset start, TimeSpan duration)
    {
        if (!Slots.IsAligned(start))
        {
            throw new ArgumentException($"Start must fall on a {Slots.LengthMinutes}-minute slot boundary.", nameof(start));
        }

        if (!Slots.IsWholeNumberOfSlots(duration))
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration), duration, $"Duration must be a positive multiple of {Slots.LengthMinutes} minutes.");
        }

        var startUtc = start.ToUniversalTime();
        return new TimeRange(startUtc, startUtc + duration);
    }

    public IEnumerable<DateTimeOffset> SlotStarts()
    {
        for (var slot = StartUtc; slot < EndUtc; slot += Slots.Length)
        {
            yield return slot;
        }
    }
}
