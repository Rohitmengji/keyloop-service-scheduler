using System.Net;
using System.Text.Json;
using ServiceScheduler.Infrastructure.Persistence;
using ServiceScheduler.IntegrationTests.Support;

namespace ServiceScheduler.IntegrationTests;

public sealed class AvailabilityTests : ApiTestBase
{
    [Fact]
    public async Task An_empty_day_offers_every_start_that_fits_opening_hours()
    {
        var slots = await GetSlotsAsync(DemoData.ServiceTypes.OilChange, "2026-11-02");

        Assert.Equal(37, slots.Count); // 08:00 to 17:00 every 15 minutes for a 60-minute service
        Assert.Equal(Monday(8), slots[0].Text("start"));
        Assert.Equal(Monday(17), slots[^1].Text("start"));
        Assert.All(slots, slot =>
        {
            Assert.Equal("2", slot.Text("freeServiceBays")); // Bay 3 is inactive
            Assert.Equal("3", slot.Text("freeTechnicians"));
        });
    }

    [Fact]
    public async Task Fully_booked_times_disappear_while_back_to_back_starts_remain()
    {
        await BookSuccessfullyAsync(SamFocusOilChange(Monday(9)));
        await BookSuccessfullyAsync(
            Booking(DemoData.Customers.EmmaWilson, DemoData.Vehicles.EmmaGolf, DemoData.ServiceTypes.OilChange, Monday(9)));

        var starts = (await GetSlotsAsync(DemoData.ServiceTypes.OilChange, "2026-11-02")).Select(s => s.Text("start")).ToList();

        Assert.Equal(30, starts.Count);
        Assert.Contains(Monday(8), starts);  // finishes as the booked hour begins
        Assert.Contains(Monday(10), starts); // starts as the booked hour ends
        foreach (var overlapping in new[] { Monday(8, 15), Monday(8, 45), Monday(9), Monday(9, 45) })
        {
            Assert.DoesNotContain(overlapping, starts);
        }
    }

    [Fact]
    public async Task Without_a_date_the_next_local_day_is_shown()
    {
        using var response = await Client.GetAsync(Url(DemoData.ServiceTypes.OilChange, date: null), Ct);

        Assert.Equal("2026-11-02", (await ReadAsync(response, HttpStatusCode.OK)).Text("date"));
    }

    [Fact]
    public async Task Times_that_have_already_passed_today_are_not_offered()
    {
        var slots = await GetSlotsAsync(DemoData.ServiceTypes.OilChange, "2026-11-01"); // the clock reads 12:00

        Assert.Equal("2026-11-01T12:15:00+00:00", slots[0].Text("start"));
        Assert.Equal(20, slots.Count);
    }

    [Fact]
    public async Task Invalid_requests_are_rejected_with_specific_problems()
    {
        using var unknownDealership = await Client.GetAsync(Url(DemoData.ServiceTypes.OilChange, dealershipId: Guid.NewGuid()), Ct);
        using var unknownService = await Client.GetAsync(Url(Guid.NewGuid()), Ct);
        using var missingService = await Client.GetAsync($"/api/v1/dealerships/{DemoData.Dealerships.Manchester}/availability", Ct);

        await AssertProblemAsync(unknownDealership, HttpStatusCode.NotFound, "dealership_not_found");
        await AssertProblemAsync(unknownService, HttpStatusCode.UnprocessableEntity, "unknown_service_type");
        await AssertProblemAsync(missingService, HttpStatusCode.BadRequest, "validation_failed");
    }

    private static string Url(Guid serviceTypeId, string? date = "2026-11-02", Guid? dealershipId = null) =>
        $"/api/v1/dealerships/{dealershipId ?? DemoData.Dealerships.Manchester}/availability?serviceTypeId={serviceTypeId}"
        + (date is null ? string.Empty : $"&date={date}");

    private async Task<List<JsonElement>> GetSlotsAsync(Guid serviceTypeId, string date)
    {
        using var response = await Client.GetAsync(Url(serviceTypeId, date), Ct);
        return [.. (await ReadAsync(response, HttpStatusCode.OK)).GetProperty("slots").EnumerateArray()];
    }
}
