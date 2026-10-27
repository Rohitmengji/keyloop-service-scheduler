using System.Globalization;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.UnitTests.Scheduling;

public sealed class SlotGridTests
{
    [Theory]
    [InlineData("2026-11-02T09:00:00+00:00", true)]
    [InlineData("2026-11-02T09:15:00+00:00", true)]
    [InlineData("2026-11-02T14:45:00+05:30", true)]
    [InlineData("2026-11-02T09:10:00+00:00", false)]
    [InlineData("2026-11-02T09:00:30+00:00", false)]
    public void Starts_must_fall_on_the_15_minute_grid(string instant, bool aligned) =>
        Assert.Equal(aligned, Slots.IsAligned(DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture)));

    [Fact]
    public void A_range_lists_one_slot_per_15_minutes_and_excludes_its_end()
    {
        var range = TimeRange.Create(Fixtures.Utc(2026, 11, 2, 9), TimeSpan.FromMinutes(90));

        var slots = range.SlotStarts().ToList();

        Assert.Equal(6, slots.Count);
        Assert.Equal(Fixtures.Utc(2026, 11, 2, 9), slots[0]);
        Assert.Equal(Fixtures.Utc(2026, 11, 2, 10, 15), slots[^1]);
        Assert.Equal(Fixtures.Utc(2026, 11, 2, 10, 30), range.EndUtc);
    }

    [Fact]
    public void A_range_is_normalised_to_utc()
    {
        var range = TimeRange.Create(
            new DateTimeOffset(2026, 11, 2, 14, 30, 0, TimeSpan.FromMinutes(330)), TimeSpan.FromMinutes(30));

        Assert.Equal(TimeSpan.Zero, range.StartUtc.Offset);
        Assert.Equal(Fixtures.Utc(2026, 11, 2, 9), range.StartUtc);
    }

    [Fact]
    public void Ranges_reject_unaligned_starts_and_partial_slots()
    {
        Assert.Throws<ArgumentException>(() => TimeRange.Create(Fixtures.Utc(2026, 11, 2, 9, 5), TimeSpan.FromHours(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeRange.Create(Fixtures.Utc(2026, 11, 2, 9), TimeSpan.FromMinutes(50)));
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeRange.Create(Fixtures.Utc(2026, 11, 2, 9), TimeSpan.Zero));
    }
}

public sealed class ResourceOccupancyTests
{
    private static readonly Guid Bay = Guid.NewGuid();

    private static ResourceOccupancy BayBookedFrom9To10() => new(
        TimeRange.Create(Fixtures.Utc(2026, 11, 2, 9), TimeSpan.FromHours(1))
            .SlotStarts()
            .Select(slot => new ResourceSlot(ResourceKind.ServiceBay, Bay, slot)));

    [Theory]
    [InlineData(8, 0, true)]   // 08:00-09:00 ends as the booking starts
    [InlineData(10, 0, true)]  // 10:00-11:00 starts as the booking ends
    [InlineData(8, 15, false)] // 08:15-09:15 overlaps the first slot
    [InlineData(9, 45, false)] // 09:45-10:45 overlaps the last slot
    [InlineData(9, 0, false)]  // identical
    public void A_resource_is_free_only_if_no_slot_of_the_range_is_reserved(int hour, int minute, bool free)
    {
        var requested = TimeRange.Create(Fixtures.Utc(2026, 11, 2, hour, minute), TimeSpan.FromHours(1));

        Assert.Equal(free, BayBookedFrom9To10().IsFree(ResourceKind.ServiceBay, Bay, requested));
    }

    [Fact]
    public void Reservations_only_block_the_same_kind_and_id()
    {
        var requested = TimeRange.Create(Fixtures.Utc(2026, 11, 2, 9), TimeSpan.FromHours(1));
        var occupancy = BayBookedFrom9To10();

        Assert.True(occupancy.IsFree(ResourceKind.ServiceBay, Guid.NewGuid(), requested));
        Assert.True(occupancy.IsFree(ResourceKind.Technician, Bay, requested));
    }
}
