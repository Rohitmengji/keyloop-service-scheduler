using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceScheduler.Domain.Catalog;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Infrastructure.Persistence.Configurations;

internal sealed class DealershipConfiguration : IEntityTypeConfiguration<Dealership>
{
    public void Configure(EntityTypeBuilder<Dealership> builder)
    {
        builder.ToTable("Dealerships", t => t.HasCheckConstraint("CK_Dealerships_OpeningHours", "\"OpensAt\" < \"ClosesAt\""));
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Name).HasMaxLength(200);
        builder.Property(d => d.TimeZoneId).HasMaxLength(64);
    }
}

internal sealed class ServiceBayConfiguration : IEntityTypeConfiguration<ServiceBay>
{
    public void Configure(EntityTypeBuilder<ServiceBay> builder)
    {
        builder.ToTable("ServiceBays");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Name).HasMaxLength(100);
        builder.HasIndex(b => new { b.DealershipId, b.Name }).IsUnique();
        builder.HasOne<Dealership>().WithMany().HasForeignKey(b => b.DealershipId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TechnicianConfiguration : IEntityTypeConfiguration<Technician>
{
    public void Configure(EntityTypeBuilder<Technician> builder)
    {
        builder.ToTable("Technicians");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.FullName).HasMaxLength(200);
        builder.HasOne<Dealership>().WithMany().HasForeignKey(t => t.DealershipId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(t => t.Qualifications).WithOne().HasForeignKey(q => q.TechnicianId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Qualifications).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class TechnicianQualificationConfiguration : IEntityTypeConfiguration<TechnicianQualification>
{
    public void Configure(EntityTypeBuilder<TechnicianQualification> builder)
    {
        builder.ToTable("TechnicianQualifications");
        builder.HasKey(q => new { q.TechnicianId, q.ServiceTypeId });
        builder.HasOne<ServiceType>().WithMany().HasForeignKey(q => q.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ServiceTypeConfiguration : IEntityTypeConfiguration<ServiceType>
{
    public void Configure(EntityTypeBuilder<ServiceType> builder)
    {
        builder.ToTable("ServiceTypes", t => t.HasCheckConstraint(
            "CK_ServiceTypes_Duration",
            $"\"DurationMinutes\" > 0 AND \"DurationMinutes\" <= {ServiceType.MaxDurationMinutes} AND \"DurationMinutes\" % {Slots.LengthMinutes} = 0"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Code).HasMaxLength(32);
        builder.HasIndex(s => s.Code).IsUnique();
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Ignore(s => s.Duration);
    }
}
