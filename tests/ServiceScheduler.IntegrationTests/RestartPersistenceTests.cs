using System.Net;
using System.Net.Http.Json;
using ServiceScheduler.Infrastructure.Persistence;
using ServiceScheduler.IntegrationTests.Support;

namespace ServiceScheduler.IntegrationTests;

public sealed class RestartPersistenceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly object SamFocusOilChange = new
    {
        dealershipId = DemoData.Dealerships.Manchester,
        customerId = DemoData.Customers.SamTaylor,
        vehicleId = DemoData.Vehicles.SamFocus,
        serviceTypeId = DemoData.ServiceTypes.OilChange,
        startTime = "2026-11-02T09:00:00+00:00",
    };

    [Fact]
    public async Task Appointments_and_their_reservations_survive_an_application_restart()
    {
        var databasePath = SchedulerApiFactory.NewDatabasePath();
        try
        {
            string booked;
            string location;
            await using (var beforeRestart = new SchedulerApiFactory(databasePath))
            {
                using var client = beforeRestart.CreateClient();
                using var response = await client.PostAsJsonAsync("/api/v1/appointments", SamFocusOilChange, Ct);
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                booked = await response.Content.ReadAsStringAsync(Ct);
                location = response.Headers.Location!.PathAndQuery;
            }

            // A new host on the same file re-runs start-up (migrations, seeding check) like a real restart.
            await using var afterRestart = new SchedulerApiFactory(databasePath);
            using var restarted = afterRestart.CreateClient();

            using var stored = await restarted.GetAsync(location, Ct);
            Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
            Assert.Equal(booked, await stored.Content.ReadAsStringAsync(Ct));

            using var duplicate = await restarted.PostAsJsonAsync("/api/v1/appointments", SamFocusOilChange, Ct);
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        }
        finally
        {
            SchedulerApiFactory.DeleteDatabase(databasePath);
        }
    }
}
