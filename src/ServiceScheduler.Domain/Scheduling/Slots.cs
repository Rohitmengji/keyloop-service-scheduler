namespace ServiceScheduler.Domain.Scheduling;

/// <summary>
/// The fixed planning grid. Appointments start on a slot boundary and occupy whole slots, which is what
/// allows the database to reject any overlap with a primary key on (resource, slot).
/// </summary>
public static class Slots
{
    public const int LengthMinutes = 15;

    public static readonly TimeSpan Length = TimeSpan.FromMinutes(LengthMinutes);

    public static bool IsAligned(DateTimeOffset instant) => instant.UtcTicks % Length.Ticks == 0;

    public static bool IsWholeNumberOfSlots(TimeSpan duration) =>
        duration > TimeSpan.Zero && duration.Ticks % Length.Ticks == 0;
}
