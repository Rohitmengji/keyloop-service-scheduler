using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceScheduler.Application.Abstractions;
using ServiceScheduler.Infrastructure.Persistence;

namespace ServiceScheduler.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Scheduler";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Resolved when the context is created, so configuration overrides (tests, environment) always apply.
        services.AddDbContext<SchedulerDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
            options.UseSqlite(connectionString);
        });
        services.AddScoped<ISchedulerDbContext>(provider => provider.GetRequiredService<SchedulerDbContext>());
        services.AddSingleton<IConstraintViolationDetector, SqliteConstraintViolationDetector>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}
