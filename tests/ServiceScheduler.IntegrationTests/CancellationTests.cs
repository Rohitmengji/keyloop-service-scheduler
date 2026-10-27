using System.Net;
using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Infrastructure.Persistence;
using ServiceScheduler.IntegrationTests.Support;

namespace ServiceScheduler.IntegrationTests;

public sealed class CancellationTests : ApiTestBase
{
    [Fact]
    public async Task Cancelling_releases_the_slots_so_the_time_can_be_booked_again()
    {
        var booked = await BookSuccessfullyAsync(
            Booking(DemoData.Customers.SamTaylor, DemoData.Vehicles.SamLeaf, DemoData.ServiceTypes.EvHealthCheck, Monday(9)));
        var id = Guid.Parse(booked.Text("id")!);

        using var cancel = await Client.PostAsync($"{AppointmentsUrl}/{id}/cancel", null, Ct);
        var cancelled = await ReadAsync(cancel, HttpStatusCode.OK);

        Assert.Equal("Cancelled", cancelled.Text("status"));
        Assert.Equal("2026-11-01T12:00:00+00:00", cancelled.Text("cancelledAtUtc"));
        Assert.Equal(0, await Factory.QueryAsync(db => db.SlotReservations.CountAsync(r => r.AppointmentId == id, Ct)));

        // Chloe is the only EV-qualified technician, so this only succeeds if her slots were released.
        var rebooked = await BookSuccessfullyAsync(
            Booking(DemoData.Customers.EmmaWilson, DemoData.Vehicles.EmmaModel3, DemoData.ServiceTypes.EvHealthCheck, Monday(9)));
        Assert.Equal("Chloe Davies", rebooked.Text("technician.name"));
    }

    [Fact]
    public async Task Cancelling_twice_returns_the_same_cancelled_appointment()
    {
        var id = (await BookSuccessfullyAsync(SamFocusOilChange(Monday(9)))).Text("id");

        using var first = await Client.PostAsync($"{AppointmentsUrl}/{id}/cancel", null, Ct);
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        using var second = await Client.PostAsync($"{AppointmentsUrl}/{id}/cancel", null, Ct);

        Assert.Equal(
            (await ReadAsync(first, HttpStatusCode.OK)).ToString(),
            (await ReadAsync(second, HttpStatusCode.OK)).ToString());
    }

    [Fact]
    public async Task Simultaneous_cancellations_all_succeed()
    {
        var id = (await BookSuccessfullyAsync(SamFocusOilChange(Monday(9)))).Text("id");

        var statuses = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            using var response = await Client.PostAsync($"{AppointmentsUrl}/{id}/cancel", null, Ct);
            return (await ReadAsync(response, HttpStatusCode.OK)).Text("status");
        }));

        Assert.All(statuses, status => Assert.Equal("Cancelled", status));
    }

    [Fact]
    public async Task An_appointment_that_has_started_cannot_be_cancelled()
    {
        var id = (await BookSuccessfullyAsync(SamFocusOilChange(Monday(9)))).Text("id");
        Factory.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 2, 9, 5, 0, TimeSpan.Zero));

        using var response = await Client.PostAsync($"{AppointmentsUrl}/{id}/cancel", null, Ct);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "appointment_already_started");
    }

    [Fact]
    public async Task Unknown_appointments_are_not_found()
    {
        var unknown = Guid.NewGuid();

        using var get = await Client.GetAsync($"{AppointmentsUrl}/{unknown}", Ct);
        using var cancel = await Client.PostAsync($"{AppointmentsUrl}/{unknown}/cancel", null, Ct);

        await AssertProblemAsync(get, HttpStatusCode.NotFound, "appointment_not_found");
        await AssertProblemAsync(cancel, HttpStatusCode.NotFound, "appointment_not_found");
    }
}
