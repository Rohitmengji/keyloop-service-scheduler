using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ServiceScheduler.Domain.Appointments;
using ServiceScheduler.Domain.Catalog;
using ServiceScheduler.Domain.Customers;
using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.Application.Abstractions;

/// <summary>
/// The persistence port used by use cases. Exposing EF Core sets keeps queries explicit and efficient;
/// provider-specific behaviour stays in the infrastructure layer.
/// </summary>
public interface ISchedulerDbContext
{
    DbSet<Dealership> Dealerships { get; }

    DbSet<ServiceBay> ServiceBays { get; }

    DbSet<Technician> Technicians { get; }

    DbSet<ServiceType> ServiceTypes { get; }

    DbSet<Customer> Customers { get; }

    DbSet<Vehicle> Vehicles { get; }

    DbSet<Appointment> Appointments { get; }

    DbSet<SlotReservation> SlotReservations { get; }

    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Recognises the database provider's unique/primary-key violation, which signals a lost booking race.</summary>
public interface IConstraintViolationDetector
{
    bool IsUniqueViolation(DbUpdateException exception);
}
