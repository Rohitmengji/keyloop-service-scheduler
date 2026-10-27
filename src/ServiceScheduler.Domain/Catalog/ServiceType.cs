using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Domain.Catalog;

public sealed class ServiceType
{
    public const int MaxDurationMinutes = 8 * 60;

    private ServiceType()
    {
    }

    public ServiceType(Guid id, string code, string name, int durationMinutes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (durationMinutes <= 0 || durationMinutes > MaxDurationMinutes || durationMinutes % Slots.LengthMinutes != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationMinutes),
                durationMinutes,
                $"Duration must be a multiple of {Slots.LengthMinutes} minutes, up to {MaxDurationMinutes} minutes.");
        }

        Id = id;
        Code = code;
        Name = name;
        DurationMinutes = durationMinutes;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public int DurationMinutes { get; private set; }

    public TimeSpan Duration => TimeSpan.FromMinutes(DurationMinutes);
}
