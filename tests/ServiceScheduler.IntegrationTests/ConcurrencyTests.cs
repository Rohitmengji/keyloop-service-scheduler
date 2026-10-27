using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceScheduler.Application.Abstractions;
using ServiceScheduler.Domain.Appointments;
using ServiceScheduler.Domain.Scheduling;
using ServiceScheduler.Infrastructure.Persistence;
using ServiceScheduler.IntegrationTests.Support;

namespace ServiceScheduler.IntegrationTests;

/// <summary>
/// Races real HTTP requests against a real SQLite file. These would pass against a mocked repository even with
/// the double-booking guard removed, which is why they run against the database.
/// </summary>
public sealed class ConcurrencyTests : ApiTestBase
{
    [Fact]
    public async Task Simultaneous_requests_for_the_last_bays_never_double_book()
    {
        var owners = await Factory.AddCustomersWithVehiclesAsync(8, Ct);

        var results = await BookConcurrentlyAsync(owners.Select(o =>
            (Booking(o.CustomerId, o.VehicleId, DemoData.ServiceTypes.OilChange, Monday(9)), (string?)null)));

        Assert.Equal(2, results.Count(r => r.Status == HttpStatusCode.Created));
        Assert.All(results.Where(r => r.Status != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.Status));
        var appointments = await Factory.QueryAsync(db => db.Appointments.ToListAsync(Ct));
        Assert.Equal(2, appointments.Count);
        Assert.Equal(2, appointments.Select(a => a.ServiceBayId).Distinct().Count());
        Assert.Equal(2, appointments.Select(a => a.TechnicianId).Distinct().Count());
    }

    [Fact]
    public async Task Simultaneous_requests_for_the_only_qualified_technician_produce_exactly_one_booking()
    {
        var owners = await Factory.AddCustomersWithVehiclesAsync(6, Ct);

        var results = await BookConcurrentlyAsync(owners.Select(o =>
            (Booking(o.CustomerId, o.VehicleId, DemoData.ServiceTypes.EvHealthCheck, Monday(9)), (string?)null)));

        Assert.Single(results, r => r.Status == HttpStatusCode.Created);
        Assert.All(results.Where(r => r.Status != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.Status));
        Assert.Equal(1, await Factory.QueryAsync(db => db.Appointments.CountAsync(Ct)));
    }

    [Fact]
    public async Task Simultaneous_retries_with_one_idempotency_key_create_one_appointment()
    {
        var results = await BookConcurrentlyAsync(
            Enumerable.Range(0, 6).Select(_ => (SamFocusOilChange(Monday(9)), (string?)"retry-storm")));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.Created, r.Status));
        Assert.Single(results.Select(r => r.Body.Text("id")).Distinct());
        Assert.Single(results, r => !r.Replayed);
        Assert.Equal(1, await Factory.QueryAsync(db => db.Appointments.CountAsync(Ct)));
    }

    [Fact]
    public async Task The_database_rejects_a_second_claim_on_a_slot_and_rolls_back_the_whole_booking()
    {
        // Bypass the application layer entirely: two hand-built appointments share Alice's 09:00 slots but not a bay.
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        var manchester = await db.Dealerships.AsNoTracking().SingleAsync(d => d.Id == DemoData.Dealerships.Manchester, Ct);
        var oilChange = await db.ServiceTypes.AsNoTracking().SingleAsync(s => s.Id == DemoData.ServiceTypes.OilChange, Ct);
        var alice = await db.Technicians.AsNoTracking().Include(t => t.Qualifications)
            .SingleAsync(t => t.Id == DemoData.Technicians.AliceMorgan, Ct);
        var bay1 = await db.ServiceBays.AsNoTracking().SingleAsync(b => b.Id == DemoData.ServiceBays.ManchesterBay1, Ct);
        var bay2 = await db.ServiceBays.AsNoTracking().SingleAsync(b => b.Id == DemoData.ServiceBays.ManchesterBay2, Ct);
        var sam = await db.Customers.AsNoTracking().SingleAsync(c => c.Id == DemoData.Customers.SamTaylor, Ct);
        var emma = await db.Customers.AsNoTracking().SingleAsync(c => c.Id == DemoData.Customers.EmmaWilson, Ct);
        var focus = await db.Vehicles.AsNoTracking().SingleAsync(v => v.Id == DemoData.Vehicles.SamFocus, Ct);
        var golf = await db.Vehicles.AsNoTracking().SingleAsync(v => v.Id == DemoData.Vehicles.EmmaGolf, Ct);
        var slot = TimeRange.Create(DateTimeOffset.Parse(Monday(9), CultureInfo.InvariantCulture), oilChange.Duration);

        db.Appointments.Add(Appointment.Confirm(
            Guid.NewGuid(), new AppointmentAllocation(manchester, sam, focus, oilChange, bay1, alice, slot), SchedulerApiFactory.StartTime));
        await db.SaveChangesAsync(Ct);
        // Real requests each use a fresh context; clearing it stops EF's identity map from catching the clash before SQLite does.
        db.ChangeTracker.Clear();

        var clash = Appointment.Confirm(
            Guid.NewGuid(), new AppointmentAllocation(manchester, emma, golf, oilChange, bay2, alice, slot), SchedulerApiFactory.StartTime);
        db.Appointments.Add(clash);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));

        Assert.True(scope.ServiceProvider.GetRequiredService<IConstraintViolationDetector>().IsUniqueViolation(exception));
        var persisted = await Factory.QueryAsync(async fresh => (
            Appointments: await fresh.Appointments.CountAsync(Ct),
            ClashReservations: await fresh.SlotReservations.CountAsync(r => r.AppointmentId == clash.Id, Ct),
            Bay2Reservations: await fresh.SlotReservations.CountAsync(r => r.ResourceId == DemoData.ServiceBays.ManchesterBay2, Ct)));
        Assert.Equal((1, 0, 0), persisted); // not even the free Bay 2 slots of the failed booking were written
    }

    private async Task<(HttpStatusCode Status, JsonElement Body, bool Replayed)[]> BookConcurrentlyAsync(
        IEnumerable<(object Body, string? IdempotencyKey)> requests) =>
        await Task.WhenAll(requests.Select(async request =>
        {
            using var response = await BookAsync(request.Body, request.IdempotencyKey);
            var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();
            return (response.StatusCode, body, response.Headers.Contains("Idempotent-Replayed"));
        }));
}
