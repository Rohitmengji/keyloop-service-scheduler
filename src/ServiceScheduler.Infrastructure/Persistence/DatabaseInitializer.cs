using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ServiceScheduler.Infrastructure.Persistence;

public sealed partial class DatabaseInitializer(SchedulerDbContext db, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(bool seedDemoData, CancellationToken cancellationToken)
    {
        await db.Database.MigrateAsync(cancellationToken);

        // WAL lets reads continue while a booking transaction writes; the mode is persisted in the database file.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", cancellationToken);

        if (seedDemoData && !await db.Dealerships.AnyAsync(cancellationToken))
        {
            db.AddRange(DemoData.Create());
            await db.SaveChangesAsync(cancellationToken);
            LogDemoDataSeeded(logger);
        }
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Seeded demo reference data")]
    private static partial void LogDemoDataSeeded(ILogger logger);
}
