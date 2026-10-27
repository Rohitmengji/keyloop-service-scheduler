using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ServiceScheduler.Application;
using ServiceScheduler.Infrastructure.Persistence;

namespace ServiceScheduler.Api.Hosting;

internal static class HostingExtensions
{
    public static IServiceCollection AddSchedulerOpenTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var telemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("service-scheduler"))
            .WithTracing(tracing => tracing
                .AddSource(SchedulerTelemetry.SourceName)
                .AddAspNetCoreInstrumentation(aspNetCore =>
                    aspNetCore.Filter = context => !context.Request.Path.StartsWithSegments("/health")))
            .WithMetrics(metrics => metrics
                .AddMeter(SchedulerTelemetry.SourceName)
                .AddAspNetCoreInstrumentation())
            .WithLogging();

        // Export only when a collector is configured (New Relic, Grafana, Aspire dashboard, ...), so the API runs
        // locally with no telemetry backend. The exporter reads the standard OTEL_EXPORTER_OTLP_* variables.
        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.UseOtlpExporter();
        }

        return services;
    }

    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        var settings = app.Configuration.GetSection("Database");
        if (!settings.GetValue<bool>("MigrateOnStartup"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync(settings.GetValue<bool>("SeedDemoData"), app.Lifetime.ApplicationStopping);
    }
}
