using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Application.Abstractions;
using ServiceScheduler.Application.Catalog;
using ServiceScheduler.Domain.Appointments;
using ServiceScheduler.Domain.Catalog;
using ServiceScheduler.Domain.Customers;
using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.Application.Appointments;

public sealed record VehicleSummary(Guid Id, string Registration, string Make, string Model);

/// <summary>An appointment as returned by the API. Start and End carry the dealership's local UTC offset.</summary>
public sealed record AppointmentDto(
    Guid Id,
    AppointmentStatus Status,
    DateTimeOffset Start,
    DateTimeOffset End,
    string TimeZone,
    NamedReference Dealership,
    NamedReference Customer,
    VehicleSummary Vehicle,
    ServiceTypeDto ServiceType,
    NamedReference ServiceBay,
    NamedReference Technician,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CancelledAtUtc)
{
    internal static AppointmentDto From(
        Appointment appointment,
        Dealership dealership,
        Customer customer,
        Vehicle vehicle,
        ServiceType serviceType,
        ServiceBay serviceBay,
        Technician technician) =>
        new(
            appointment.Id,
            appointment.Status,
            dealership.ToLocalTime(appointment.StartUtc),
            dealership.ToLocalTime(appointment.EndUtc),
            dealership.TimeZoneId,
            new NamedReference(dealership.Id, dealership.Name),
            new NamedReference(customer.Id, customer.FullName),
            new VehicleSummary(vehicle.Id, vehicle.Registration, vehicle.Make, vehicle.Model),
            ServiceTypeDto.From(serviceType),
            new NamedReference(serviceBay.Id, serviceBay.Name),
            new NamedReference(technician.Id, technician.FullName),
            appointment.Notes,
            appointment.CreatedAtUtc,
            appointment.CancelledAtUtc);
}

public sealed class AppointmentQueries(ISchedulerDbContext db)
{
    public async Task<AppointmentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await (
                from a in db.Appointments.AsNoTracking()
                join d in db.Dealerships on a.DealershipId equals d.Id
                join c in db.Customers on a.CustomerId equals c.Id
                join v in db.Vehicles on a.VehicleId equals v.Id
                join s in db.ServiceTypes on a.ServiceTypeId equals s.Id
                join b in db.ServiceBays on a.ServiceBayId equals b.Id
                join t in db.Technicians on a.TechnicianId equals t.Id
                where a.Id == id
                select new { a, d, c, v, s, b, t })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : AppointmentDto.From(row.a, row.d, row.c, row.v, row.s, row.b, row.t);
    }
}
