using ServiceScheduler.Domain.Appointments;
using ServiceScheduler.Domain.Catalog;
using ServiceScheduler.Domain.Customers;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.UnitTests;

internal static class Fixtures
{
    /// <summary>Sunday 1 November 2026, 12:00 UTC. The UK is on GMT, so London local time equals UTC.</summary>
    public static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    public static DateTimeOffset Utc(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    public static Dealership London() =>
        new(Guid.NewGuid(), "London", "Europe/London", new TimeOnly(8, 0), new TimeOnly(18, 0));

    public static Dealership Pune() =>
        new(Guid.NewGuid(), "Pune", "Asia/Kolkata", new TimeOnly(9, 0), new TimeOnly(18, 0));

    /// <summary>A valid allocation at a London dealership: Monday 2 November 2026, 09:00-10:00 by default.</summary>
    public static AppointmentAllocation Allocation(DateTimeOffset? start = null, int durationMinutes = 60)
    {
        var dealership = London();
        var serviceType = new ServiceType(Guid.NewGuid(), "OIL", "Oil change", durationMinutes);
        var customer = new Customer(Guid.NewGuid(), "Test Customer", "test.customer@example.com");
        var vehicle = new Vehicle(Guid.NewGuid(), customer.Id, "WVWZZZ1KZLW000001", "AB12 CDE", "Volkswagen", "Golf", 2020);
        var bay = new ServiceBay(Guid.NewGuid(), dealership.Id, "Bay 1");
        var technician = new Technician(Guid.NewGuid(), dealership.Id, "Test Technician");
        technician.AddQualification(serviceType.Id);
        var slot = TimeRange.Create(start ?? Utc(2026, 11, 2, 9), serviceType.Duration);
        return new AppointmentAllocation(dealership, customer, vehicle, serviceType, bay, technician, slot);
    }
}
