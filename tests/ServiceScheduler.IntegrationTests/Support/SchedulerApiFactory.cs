using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using ServiceScheduler.Domain.Customers;
using ServiceScheduler.Infrastructure.Persistence;

namespace ServiceScheduler.IntegrationTests.Support;

/// <summary>
/// Hosts the real API in memory against its own SQLite file, migrated and seeded exactly as in development,
/// with a controllable clock so date-based rules are deterministic.
/// </summary>
/// <param name="databasePath">Reuse an existing database file (to simulate a restart); the caller then owns its deletion.</param>
public sealed class SchedulerApiFactory(string? databasePath = null) : WebApplicationFactory<Program>
{
    /// <summary>Sunday 1 November 2026, 12:00 UTC. The UK is on GMT, so London local time equals UTC.</summary>
    public static readonly DateTimeOffset StartTime = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly string _databasePath = databasePath ?? NewDatabasePath();
    private readonly bool _ownsDatabase = databasePath is null;

    public static string NewDatabasePath() => Path.Combine(Path.GetTempPath(), $"scheduler-tests-{Guid.NewGuid():N}.db");

    public static void DeleteDatabase(string path)
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            File.Delete(path + suffix);
        }
    }

    public FakeTimeProvider Clock { get; } = new(StartTime);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Scheduler", $"Data Source={_databasePath}");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Database:SeedDemoData", "true");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Clock));
    }

    public async Task<T> QueryAsync<T>(Func<SchedulerDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<SchedulerDbContext>());
    }

    /// <summary>Adds customers that each own one vehicle, for tests that need many distinct bookings.</summary>
    public async Task<IReadOnlyList<(Guid CustomerId, Guid VehicleId)>> AddCustomersWithVehiclesAsync(
        int count, CancellationToken cancellationToken)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        var owners = new List<(Guid, Guid)>();
        for (var i = 0; i < count; i++)
        {
            var customer = new Customer(Guid.NewGuid(), $"Test Customer {i}", $"customer{i}@example.com");
            var vehicle = new Vehicle(Guid.NewGuid(), customer.Id, $"TESTVIN{i:D10}", $"TS{i:D2} TST", "Test", "Car", 2024);
            db.AddRange(customer, vehicle);
            owners.Add((customer.Id, vehicle.Id));
        }

        await db.SaveChangesAsync(cancellationToken);
        return owners;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_ownsDatabase)
        {
            DeleteDatabase(_databasePath);
        }
        else
        {
            SqliteConnection.ClearAllPools();
        }
    }
}
