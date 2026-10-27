using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Domain.Scheduling;
using ServiceScheduler.Infrastructure.Persistence;
using ServiceScheduler.IntegrationTests.Support;

namespace ServiceScheduler.IntegrationTests;

public sealed class BookingTests : ApiTestBase
{
    [Fact]
    public async Task Booking_a_free_slot_confirms_a_persistent_appointment_with_a_bay_and_a_qualified_technician()
    {
        using var response = await BookAsync(SamFocusOilChange(Monday(9)));
        var appointment = await ReadAsync(response, HttpStatusCode.Created);

        Assert.Equal("Confirmed", appointment.Text("status"));
        Assert.Equal(Monday(9), appointment.Text("start"));
        Assert.Equal(Monday(10), appointment.Text("end"));
        Assert.Equal("Europe/London", appointment.Text("timeZone"));
        Assert.Equal("Sam Taylor", appointment.Text("customer.name"));
        Assert.Equal("MA19 KLP", appointment.Text("vehicle.registration"));
        Assert.Equal("OIL", appointment.Text("serviceType.code"));
        Assert.Equal("Bay 1", appointment.Text("serviceBay.name"));
        Assert.Equal("Alice Morgan", appointment.Text("technician.name"));

        // The Location header points at the stored record, which reads back identically.
        using var stored = await Client.GetAsync(response.Headers.Location, Ct);
        Assert.Equal(appointment.ToString(), (await ReadAsync(stored, HttpStatusCode.OK)).ToString());

        var id = Guid.Parse(appointment.Text("id")!);
        var reservations = await Factory.QueryAsync(db => db.SlotReservations.Where(r => r.AppointmentId == id).ToListAsync(Ct));
        Assert.Equal(12, reservations.Count); // 4 slots x (bay, technician, vehicle)
        Assert.Equal(4, reservations.Count(r => r.ResourceKind == ResourceKind.ServiceBay && r.ResourceId == DemoData.ServiceBays.ManchesterBay1));
        Assert.Equal(4, reservations.Count(r => r.ResourceKind == ResourceKind.Technician && r.ResourceId == DemoData.Technicians.AliceMorgan));
        Assert.Equal(4, reservations.Count(r => r.ResourceKind == ResourceKind.Vehicle && r.ResourceId == DemoData.Vehicles.SamFocus));
    }

    [Fact]
    public async Task Missing_fields_are_reported_under_their_json_names()
    {
        using var response = await Client.PostAsJsonAsync(AppointmentsUrl, new { }, Ct);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        var errors = problem.GetProperty("errors");
        foreach (var field in new[] { "dealershipId", "customerId", "vehicleId", "serviceTypeId", "startTime" })
        {
            Assert.True(errors.TryGetProperty(field, out _), $"Expected a validation error for '{field}': {errors}");
        }
    }

    [Theory]
    [InlineData("2026-11-02T09:00:00")]
    [InlineData("2026-11-02 09:00")]
    [InlineData("next monday")]
    public async Task Timestamps_without_an_explicit_utc_offset_are_rejected(string startTime)
    {
        using var response = await BookAsync(SamFocusOilChange(startTime));

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("$.startTime", out _), problem.ToString());
    }

    [Fact]
    public async Task Notes_longer_than_500_characters_are_rejected()
    {
        var body = Booking(
            DemoData.Customers.SamTaylor, DemoData.Vehicles.SamFocus, DemoData.ServiceTypes.OilChange, Monday(9), notes: new string('x', 501));

        using var response = await BookAsync(body);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("notes", out _), problem.ToString());
    }

    [Theory]
    [InlineData("unknown_dealership")]
    [InlineData("unknown_customer")]
    [InlineData("unknown_vehicle")]
    [InlineData("unknown_service_type")]
    public async Task Unknown_ids_in_the_request_are_unprocessable(string expectedCode)
    {
        var unknown = Guid.NewGuid();
        var body = expectedCode switch
        {
            "unknown_dealership" => Booking(DemoData.Customers.SamTaylor, DemoData.Vehicles.SamFocus, DemoData.ServiceTypes.OilChange, Monday(9), dealershipId: unknown),
            "unknown_customer" => Booking(unknown, DemoData.Vehicles.SamFocus, DemoData.ServiceTypes.OilChange, Monday(9)),
            "unknown_vehicle" => Booking(DemoData.Customers.SamTaylor, unknown, DemoData.ServiceTypes.OilChange, Monday(9)),
            _ => Booking(DemoData.Customers.SamTaylor, DemoData.Vehicles.SamFocus, unknown, Monday(9)),
        };

        using var response = await BookAsync(body);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, expectedCode);
    }

    [Fact]
    public async Task A_customer_cannot_book_a_vehicle_registered_to_someone_else()
    {
        var body = Booking(DemoData.Customers.EmmaWilson, DemoData.Vehicles.SamFocus, DemoData.ServiceTypes.OilChange, Monday(9));

        using var response = await BookAsync(body);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "vehicle_not_owned_by_customer");
    }

    [Theory]
    [InlineData("2026-11-02T09:10:00+00:00")]
    [InlineData("2026-11-02T09:00:30+00:00")]
    public async Task Start_times_must_be_on_the_15_minute_grid(string startTime)
    {
        using var response = await BookAsync(SamFocusOilChange(startTime));

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "start_time_not_aligned");
    }

    [Theory]
    [InlineData("2026-11-01T11:00:00+00:00", "start_time_in_past")]
    [InlineData("2027-02-01T09:00:00+00:00", "beyond_booking_horizon")]
    public async Task Start_times_must_be_in_the_future_and_within_the_booking_horizon(string startTime, string expectedCode)
    {
        using var response = await BookAsync(SamFocusOilChange(startTime));

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, expectedCode);
    }

    [Theory]
    [InlineData(7, 45)]  // starts before 08:00 opening
    [InlineData(17, 15)] // would finish at 18:15, after closing
    public async Task The_whole_service_must_fit_inside_opening_hours(int hour, int minute)
    {
        using var response = await BookAsync(SamFocusOilChange(Monday(hour, minute)));

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "outside_opening_hours");
    }

    [Fact]
    public async Task A_service_may_finish_exactly_at_closing_time()
    {
        var appointment = await BookSuccessfullyAsync(SamFocusOilChange(Monday(17)));

        Assert.Equal(Monday(18), appointment.Text("end"));
    }

    [Fact]
    public async Task The_same_instant_sent_in_another_offset_is_stored_and_returned_in_dealership_time()
    {
        var appointment = await BookSuccessfullyAsync(SamFocusOilChange("2026-11-02T10:00:00+01:00"));

        Assert.Equal(Monday(9), appointment.Text("start"));
    }

    [Fact]
    public async Task Times_are_returned_in_the_dealerships_own_time_zone()
    {
        var body = Booking(
            DemoData.Customers.ArjunMehta,
            DemoData.Vehicles.ArjunCreta,
            DemoData.ServiceTypes.OilChange,
            "2026-11-02T04:00:00Z",
            dealershipId: DemoData.Dealerships.Pune);

        var appointment = await BookSuccessfullyAsync(body);

        Assert.Equal("2026-11-02T09:30:00+05:30", appointment.Text("start"));
        Assert.Equal("Asia/Kolkata", appointment.Text("timeZone"));
        Assert.Equal("Priya Nair", appointment.Text("technician.name"));
    }

    [Fact]
    public async Task A_dealership_with_no_qualified_technician_cannot_offer_the_service()
    {
        var body = Booking(
            DemoData.Customers.ArjunMehta,
            DemoData.Vehicles.ArjunCreta,
            DemoData.ServiceTypes.Mot,
            "2026-11-02T10:00:00+05:30",
            dealershipId: DemoData.Dealerships.Pune);

        using var response = await BookAsync(body);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "no_qualified_technician");
    }
}
