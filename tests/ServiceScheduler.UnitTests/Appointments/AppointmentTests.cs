using ServiceScheduler.Domain.Appointments;
using ServiceScheduler.Domain.Customers;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.UnitTests.Appointments;

public sealed class AppointmentTests
{
    [Fact]
    public void Confirming_reserves_the_bay_technician_and_vehicle_for_every_slot()
    {
        var allocation = Fixtures.Allocation(durationMinutes: 60);

        var appointment = Appointment.Confirm(Guid.NewGuid(), allocation, Fixtures.Now);

        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
        Assert.Equal(Fixtures.Utc(2026, 11, 2, 10), appointment.EndUtc);
        Assert.Equal(12, appointment.Reservations.Count); // 4 slots x 3 resources
        Assert.Equal(4, appointment.Reservations.Count(r => r.ResourceKind == ResourceKind.ServiceBay && r.ResourceId == allocation.ServiceBay.Id));
        Assert.Equal(4, appointment.Reservations.Count(r => r.ResourceKind == ResourceKind.Technician && r.ResourceId == allocation.Technician.Id));
        Assert.Equal(4, appointment.Reservations.Count(r => r.ResourceKind == ResourceKind.Vehicle && r.ResourceId == allocation.Vehicle.Id));
        Assert.All(appointment.Reservations, r => Assert.Equal(appointment.Id, r.AppointmentId));
    }

    [Fact]
    public void An_unqualified_technician_cannot_be_allocated()
    {
        var allocation = Fixtures.Allocation();
        var unqualified = new Technician(Guid.NewGuid(), allocation.Dealership.Id, "Trainee");

        AssertRejected(allocation with { Technician = unqualified }, "qualified");
    }

    [Fact]
    public void A_vehicle_registered_to_another_customer_cannot_be_booked()
    {
        var allocation = Fixtures.Allocation();
        var someoneElses = new Vehicle(Guid.NewGuid(), Guid.NewGuid(), "WF0XXXGCDXKA00009", "XY99 ZZZ", "Ford", "Focus", 2019);

        AssertRejected(allocation with { Vehicle = someoneElses }, "vehicle");
    }

    [Fact]
    public void A_bay_must_be_active_and_belong_to_the_dealership()
    {
        var allocation = Fixtures.Allocation();

        AssertRejected(allocation with { ServiceBay = new ServiceBay(Guid.NewGuid(), Guid.NewGuid(), "Elsewhere") }, "bay");
        AssertRejected(allocation with { ServiceBay = new ServiceBay(Guid.NewGuid(), allocation.Dealership.Id, "Closed", isActive: false) }, "bay");
    }

    [Fact]
    public void The_slot_must_be_in_the_future_and_inside_opening_hours()
    {
        AssertRejected(Fixtures.Allocation(start: Fixtures.Utc(2026, 11, 2, 7)), "opening hours");
        AssertRejected(Fixtures.Allocation(start: Fixtures.Utc(2026, 11, 1, 9)), "future");
    }

    [Fact]
    public void Cancelling_releases_every_slot_and_records_when()
    {
        var appointment = Appointment.Confirm(Guid.NewGuid(), Fixtures.Allocation(), Fixtures.Now);

        var error = appointment.Cancel(Fixtures.Now.AddHours(1));

        Assert.Null(error);
        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.Equal(Fixtures.Now.AddHours(1), appointment.CancelledAtUtc);
        Assert.Empty(appointment.Reservations);
        Assert.Equal(2, appointment.Version);
    }

    [Fact]
    public void Cancelling_twice_changes_nothing_the_second_time()
    {
        var appointment = Appointment.Confirm(Guid.NewGuid(), Fixtures.Allocation(), Fixtures.Now);
        appointment.Cancel(Fixtures.Now);

        var error = appointment.Cancel(Fixtures.Now.AddHours(2));

        Assert.Null(error);
        Assert.Equal(Fixtures.Now, appointment.CancelledAtUtc);
        Assert.Equal(2, appointment.Version);
    }

    [Fact]
    public void An_appointment_that_has_started_cannot_be_cancelled()
    {
        var appointment = Appointment.Confirm(Guid.NewGuid(), Fixtures.Allocation(), Fixtures.Now);

        var error = appointment.Cancel(appointment.StartUtc);

        Assert.Equal(AppointmentErrors.AlreadyStarted, error);
        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
        Assert.Equal(12, appointment.Reservations.Count);
    }

    private static void AssertRejected(AppointmentAllocation allocation, string reason)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Appointment.Confirm(Guid.NewGuid(), allocation, Fixtures.Now));
        Assert.Contains(reason, exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
