using Microsoft.Extensions.DependencyInjection;
using ServiceScheduler.Application.Appointments;
using ServiceScheduler.Application.Availability;
using ServiceScheduler.Application.Catalog;

namespace ServiceScheduler.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<SchedulerTelemetry>();
        services.AddScoped<AppointmentQueries>();
        services.AddScoped<BookAppointmentHandler>();
        services.AddScoped<CancelAppointmentHandler>();
        services.AddScoped<GetAvailabilityHandler>();
        services.AddScoped<CatalogQueries>();
        return services;
    }
}
