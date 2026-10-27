using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceScheduler.Domain.Appointments;
using ServiceScheduler.Domain.Catalog;
using ServiceScheduler.Domain.Customers;
using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.FullName).HasMaxLength(200);
        builder.Property(c => c.Email).HasMaxLength(320);
        builder.HasIndex(c => c.Email).IsUnique();
    }
}

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Vin).HasMaxLength(Vehicle.VinLength);
        builder.HasIndex(v => v.Vin).IsUnique();
        builder.Property(v => v.Registration).HasMaxLength(16);
        builder.Property(v => v.Make).HasMaxLength(64);
        builder.Property(v => v.Model).HasMaxLength(64);
        builder.HasOne<Customer>().WithMany().HasForeignKey(v => v.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments", t => t.HasCheckConstraint("CK_Appointments_EndAfterStart", "\"EndUtc\" > \"StartUtc\""));
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(a => a.Notes).HasMaxLength(Appointment.NotesMaxLength);
        builder.Property(a => a.IdempotencyKey).HasMaxLength(Appointment.IdempotencyKeyMaxLength);
        builder.Property(a => a.RequestFingerprint).HasMaxLength(64);
        builder.Property(a => a.Version).IsConcurrencyToken();

        builder.HasIndex(a => a.IdempotencyKey).IsUnique();
        builder.HasIndex(a => new { a.DealershipId, a.StartUtc });

        builder.HasOne<Dealership>().WithMany().HasForeignKey(a => a.DealershipId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(a => a.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ServiceType>().WithMany().HasForeignKey(a => a.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ServiceBay>().WithMany().HasForeignKey(a => a.ServiceBayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Technician>().WithMany().HasForeignKey(a => a.TechnicianId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Reservations).WithOne().HasForeignKey(r => r.AppointmentId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Reservations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SlotReservationConfiguration : IEntityTypeConfiguration<SlotReservation>
{
    public void Configure(EntityTypeBuilder<SlotReservation> builder)
    {
        builder.ToTable("SlotReservations");
        // The double-booking guard: at most one row per resource per slot, enforced by the database itself.
        builder.HasKey(r => new { r.ResourceKind, r.ResourceId, r.SlotStartUtc });
        builder.Property(r => r.ResourceKind).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(r => r.AppointmentId);
    }
}
