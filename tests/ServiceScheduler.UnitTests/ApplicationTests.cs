using ServiceScheduler.Application.Appointments;
using ServiceScheduler.Domain.Appointments;

namespace ServiceScheduler.UnitTests;

public sealed class RequestFingerprintTests
{
    private static readonly BookAppointmentCommand Command = new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        Fixtures.Utc(2026, 11, 2, 9), "Customer will wait", "key-1");

    [Fact]
    public void The_same_instant_written_with_another_offset_is_the_same_request()
    {
        var sameInstant = Command with { StartTime = new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.FromHours(1)) };

        Assert.Equal(RequestFingerprint.Compute(Command), RequestFingerprint.Compute(sameInstant));
    }

    [Fact]
    public void Any_change_to_what_is_booked_changes_the_fingerprint()
    {
        var original = RequestFingerprint.Compute(Command);

        Assert.NotEqual(original, RequestFingerprint.Compute(Command with { VehicleId = Guid.NewGuid() }));
        Assert.NotEqual(original, RequestFingerprint.Compute(Command with { StartTime = Command.StartTime.AddMinutes(15) }));
        Assert.NotEqual(original, RequestFingerprint.Compute(Command with { Notes = null }));
    }

    [Fact]
    public void The_idempotency_key_itself_is_not_part_of_the_fingerprint() =>
        Assert.Equal(RequestFingerprint.Compute(Command), RequestFingerprint.Compute(Command with { IdempotencyKey = "other" }));
}

public sealed class ArchitectureTests
{
    [Fact]
    public void The_domain_does_not_depend_on_other_layers_or_frameworks()
    {
        // Assert forbidden references rather than "System.* only": coverage instrumentation adds its own reference.
        var references = typeof(Appointment).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.DoesNotContain(references, name =>
            name.StartsWith("ServiceScheduler.", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.Extensions", StringComparison.Ordinal));
    }

    [Fact]
    public void The_application_layer_does_not_depend_on_web_or_database_provider_code()
    {
        var references = typeof(BookAppointmentHandler).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.DoesNotContain(references, name =>
            name.StartsWith("ServiceScheduler.Infrastructure", StringComparison.Ordinal)
            || name.StartsWith("ServiceScheduler.Api", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.Data.Sqlite", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal));
    }
}
