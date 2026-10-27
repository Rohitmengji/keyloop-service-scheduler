using System.ComponentModel.DataAnnotations;

namespace ServiceScheduler.Application;

public sealed class SchedulingOptions
{
    public const string SectionName = "Scheduling";

    /// <summary>How far ahead an appointment may start; bounds slot growth and availability queries.</summary>
    [Range(1, 365)]
    public int BookingHorizonDays { get; set; } = 90;

    /// <summary>How many times a booking re-reads availability after losing a race to a concurrent booking.</summary>
    [Range(1, 10)]
    public int MaxAllocationAttempts { get; set; } = 3;
}
