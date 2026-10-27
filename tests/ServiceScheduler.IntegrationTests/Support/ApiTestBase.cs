using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceScheduler.Infrastructure.Persistence;

namespace ServiceScheduler.IntegrationTests.Support;

/// <summary>Each test gets a fresh API instance and database, so tests never share bookings.</summary>
public abstract class ApiTestBase : IAsyncLifetime
{
    protected const string AppointmentsUrl = "/api/v1/appointments";

    protected SchedulerApiFactory Factory { get; } = new();

    protected HttpClient Client { get; private set; } = null!;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        Client = Factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>A wall-clock time on Monday 2 November 2026, written with the London (GMT) offset.</summary>
    protected static string Monday(int hour, int minute = 0) => $"2026-11-02T{hour:D2}:{minute:D2}:00+00:00";

    protected static object Booking(
        Guid customerId,
        Guid vehicleId,
        Guid serviceTypeId,
        string startTime,
        Guid? dealershipId = null,
        string? notes = null) => new
        {
            dealershipId = dealershipId ?? DemoData.Dealerships.Manchester,
            customerId,
            vehicleId,
            serviceTypeId,
            startTime,
            notes,
        };

    protected static object SamFocusOilChange(string startTime) =>
        Booking(DemoData.Customers.SamTaylor, DemoData.Vehicles.SamFocus, DemoData.ServiceTypes.OilChange, startTime);

    protected Task<HttpResponseMessage> BookAsync(object body, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, AppointmentsUrl) { Content = JsonContent.Create(body) };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return Client.SendAsync(request, Ct);
    }

    protected async Task<JsonElement> BookSuccessfullyAsync(object body)
    {
        using var response = await BookAsync(body);
        return await ReadAsync(response, HttpStatusCode.Created);
    }

    protected static async Task<JsonElement> ReadAsync(HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(
            response.StatusCode == expectedStatus,
            $"Expected HTTP {(int)expectedStatus} but got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>Asserts an RFC 9457 problem response with the expected status and machine-readable code.</summary>
    protected static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var problem = await ReadAsync(response, status);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(code, problem.Text("code"));
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.Text("traceId")));
        return problem;
    }
}

internal static class JsonElementExtensions
{
    /// <summary>Reads a nested value by dotted path, for example "serviceBay.name".</summary>
    public static string? Text(this JsonElement element, string path)
    {
        foreach (var segment in path.Split('.'))
        {
            element = element.GetProperty(segment);
        }

        return element.ValueKind == JsonValueKind.Null ? null : element.ToString();
    }
}
