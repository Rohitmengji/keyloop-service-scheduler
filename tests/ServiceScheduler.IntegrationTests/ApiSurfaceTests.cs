using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ServiceScheduler.Application.Catalog;
using ServiceScheduler.Infrastructure.Persistence;
using ServiceScheduler.IntegrationTests.Support;

namespace ServiceScheduler.IntegrationTests;

public sealed class ApiSurfaceTests : ApiTestBase
{
    [Fact]
    public async Task Dealerships_list_their_active_bays_and_qualified_technicians()
    {
        using var response = await Client.GetAsync("/api/v1/dealerships", Ct);
        var dealerships = await ReadAsync(response, HttpStatusCode.OK);

        var manchester = dealerships.EnumerateArray().Single(d => d.Text("id") == DemoData.Dealerships.Manchester.ToString());
        Assert.Equal(new[] { "Bay 1", "Bay 2" }, manchester.GetProperty("serviceBays").EnumerateArray().Select(b => b.Text("name")));
        var chloe = manchester.GetProperty("technicians").EnumerateArray().Single(t => t.Text("name") == "Chloe Davies");
        Assert.Contains("EV_HEALTH", chloe.GetProperty("qualifiedServiceCodes").EnumerateArray().Select(code => code.GetString()));
    }

    [Fact]
    public async Task Service_types_are_listed_with_their_durations()
    {
        using var response = await Client.GetAsync("/api/v1/service-types", Ct);
        var serviceTypes = (await ReadAsync(response, HttpStatusCode.OK)).EnumerateArray().ToList();

        Assert.Equal(6, serviceTypes.Count);
        Assert.Contains(serviceTypes, s => s.Text("code") == "FULL" && s.Text("durationMinutes") == "120");
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_report_healthy(string path)
    {
        using var response = await Client.GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task The_openapi_document_describes_the_booking_contract()
    {
        using var response = await Client.GetAsync("/openapi/v1.json", Ct);
        var document = await ReadAsync(response, HttpStatusCode.OK);

        var book = document.GetProperty("paths").GetProperty("/api/v1/appointments").GetProperty("post");
        Assert.Contains(book.GetProperty("parameters").EnumerateArray(), p => p.Text("name") == "Idempotency-Key" && p.Text("in") == "header");
        foreach (var status in new[] { "201", "400", "409", "422" })
        {
            Assert.True(book.GetProperty("responses").TryGetProperty(status, out _), $"POST /appointments should document {status}");
        }
    }

    [Fact]
    public async Task Unknown_routes_return_problem_details()
    {
        using var response = await Client.GetAsync("/api/v1/does-not-exist", Ct);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Unexpected_failures_return_a_generic_problem_without_internal_details()
    {
        await using var failing = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<CatalogQueries>(_ => throw new InvalidOperationException("Password=hunter2 in a connection string"))));
        using var client = failing.CreateClient();

        using var response = await client.GetAsync("/api/v1/service-types", Ct);

        var problem = (await AssertProblemAsync(response, HttpStatusCode.InternalServerError, "internal_error")).ToString();
        Assert.DoesNotContain("hunter2", problem, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejected_bookings_are_counted_by_reason()
    {
        var meterFactory = Factory.Services.GetRequiredService<IMeterFactory>();
        var reasons = new ConcurrentBag<string>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                // Only this test's host: other tests run in parallel in the same process.
                if (instrument.Meter.Scope == meterFactory && instrument.Name == "scheduler.bookings.rejected")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "reason")
                {
                    reasons.Add((string)tag.Value!);
                }
            }
        });
        listener.Start();

        await BookSuccessfullyAsync(SamFocusOilChange(Monday(9)));
        using var duplicate = await BookAsync(SamFocusOilChange(Monday(9)));

        Assert.Equal(new[] { "vehicle_unavailable" }, reasons.ToArray());
    }
}
