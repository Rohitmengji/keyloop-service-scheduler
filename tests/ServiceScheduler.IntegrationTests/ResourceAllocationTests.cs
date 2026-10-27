using System.Net;
using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Infrastructure.Persistence;
using ServiceScheduler.IntegrationTests.Support;

namespace ServiceScheduler.IntegrationTests;

public sealed class ResourceAllocationTests : ApiTestBase
{
    [Fact]
    public async Task When_every_active_bay_is_taken_the_next_booking_is_a_conflict()
    {
        var first = await BookSuccessfullyAsync(SamFocusOilChange(Monday(9)));
        var second = await BookSuccessfullyAsync(
            Booking(DemoData.Customers.EmmaWilson, DemoData.Vehicles.EmmaGolf, DemoData.ServiceTypes.OilChange, Monday(9)));

        using var third = await BookAsync(
            Booking(DemoData.Customers.ArjunMehta, DemoData.Vehicles.ArjunCreta, DemoData.ServiceTypes.OilChange, Monday(9, 30)));

        Assert.Equal("Bay 1", first.Text("serviceBay.name"));
        Assert.Equal("Bay 2", second.Text("serviceBay.name"));
        await AssertProblemAsync(third, HttpStatusCode.Conflict, "service_bay_unavailable");
        var inactiveBayUsed = await Factory.QueryAsync(db =>
            db.Appointments.AnyAsync(a => a.ServiceBayId == DemoData.ServiceBays.ManchesterBay3, Ct));
        Assert.False(inactiveBayUsed);
    }

    [Fact]
    public async Task When_the_only_qualified_technician_is_busy_an_overlapping_booking_is_a_conflict()
    {
        var first = await BookSuccessfullyAsync(
            Booking(DemoData.Customers.SamTaylor, DemoData.Vehicles.SamLeaf, DemoData.ServiceTypes.EvHealthCheck, Monday(9)));

        // 09:15-09:45 overlaps 09:00-09:30 by one slot; a bay is still free, the EV technician is not.
        using var overlapping = await BookAsync(
            Booking(DemoData.Customers.EmmaWilson, DemoData.Vehicles.EmmaModel3, DemoData.ServiceTypes.EvHealthCheck, Monday(9, 15)));

        Assert.Equal("Chloe Davies", first.Text("technician.name"));
        await AssertProblemAsync(overlapping, HttpStatusCode.Conflict, "technician_unavailable");
    }

    [Fact]
    public async Task Back_to_back_appointments_can_share_a_bay_and_technician()
    {
        var first = await BookSuccessfullyAsync(
            Booking(DemoData.Customers.SamTaylor, DemoData.Vehicles.SamLeaf, DemoData.ServiceTypes.EvHealthCheck, Monday(9)));
        var second = await BookSuccessfullyAsync(
            Booking(DemoData.Customers.EmmaWilson, DemoData.Vehicles.EmmaModel3, DemoData.ServiceTypes.EvHealthCheck, Monday(9, 30)));

        Assert.Equal(first.Text("end"), second.Text("start"));
        Assert.Equal(first.Text("technician.id"), second.Text("technician.id"));
        Assert.Equal(first.Text("serviceBay.id"), second.Text("serviceBay.id"));
    }

    [Fact]
    public async Task A_vehicle_cannot_be_booked_into_two_overlapping_appointments_even_at_different_dealerships()
    {
        await BookSuccessfullyAsync(SamFocusOilChange(Monday(9)));

        using var elsewhere = await BookAsync(Booking(
            DemoData.Customers.SamTaylor,
            DemoData.Vehicles.SamFocus,
            DemoData.ServiceTypes.OilChange,
            "2026-11-02T09:30:00Z",
            dealershipId: DemoData.Dealerships.Pune));

        await AssertProblemAsync(elsewhere, HttpStatusCode.Conflict, "vehicle_unavailable");
    }

    [Fact]
    public async Task Submitting_the_same_booking_twice_without_a_key_is_caught_by_the_vehicle_rule()
    {
        await BookSuccessfullyAsync(SamFocusOilChange(Monday(9)));

        using var duplicate = await BookAsync(SamFocusOilChange(Monday(9)));

        await AssertProblemAsync(duplicate, HttpStatusCode.Conflict, "vehicle_unavailable");
        Assert.Equal(1, await Factory.QueryAsync(db => db.Appointments.CountAsync(Ct)));
    }
}
