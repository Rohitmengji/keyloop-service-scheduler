using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Application.Abstractions;
using ServiceScheduler.Domain.Catalog;

namespace ServiceScheduler.Application.Catalog;

public sealed record NamedReference(Guid Id, string Name);

public sealed record ServiceTypeDto(Guid Id, string Code, string Name, int DurationMinutes)
{
    internal static ServiceTypeDto From(ServiceType serviceType) =>
        new(serviceType.Id, serviceType.Code, serviceType.Name, serviceType.DurationMinutes);
}

public sealed record TechnicianDto(Guid Id, string Name, IReadOnlyList<string> QualifiedServiceCodes);

public sealed record DealershipDto(
    Guid Id,
    string Name,
    string TimeZone,
    TimeOnly OpensAt,
    TimeOnly ClosesAt,
    IReadOnlyList<NamedReference> ServiceBays,
    IReadOnlyList<TechnicianDto> Technicians);

public sealed class CatalogQueries(ISchedulerDbContext db)
{
    public async Task<IReadOnlyList<ServiceTypeDto>> ListServiceTypesAsync(CancellationToken cancellationToken) =>
        await db.ServiceTypes.AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new ServiceTypeDto(s.Id, s.Code, s.Name, s.DurationMinutes))
            .ToListAsync(cancellationToken);

    /// <summary>Dealerships with their active bays and technicians, loaded in a fixed number of queries.</summary>
    public async Task<IReadOnlyList<DealershipDto>> ListDealershipsAsync(CancellationToken cancellationToken)
    {
        var dealerships = await db.Dealerships.AsNoTracking().OrderBy(d => d.Name).ToListAsync(cancellationToken);
        var bays = await db.ServiceBays.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);
        var technicians = await db.Technicians.AsNoTracking()
            .Include(t => t.Qualifications)
            .Where(t => t.IsActive)
            .OrderBy(t => t.FullName)
            .ToListAsync(cancellationToken);
        var serviceCodes = await db.ServiceTypes.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Code, cancellationToken);

        return dealerships
            .Select(d => new DealershipDto(
                d.Id,
                d.Name,
                d.TimeZoneId,
                d.OpensAt,
                d.ClosesAt,
                [.. bays.Where(b => b.DealershipId == d.Id).Select(b => new NamedReference(b.Id, b.Name))],
                [.. technicians
                    .Where(t => t.DealershipId == d.Id)
                    .Select(t => new TechnicianDto(
                        t.Id,
                        t.FullName,
                        [.. t.Qualifications.Select(q => serviceCodes[q.ServiceTypeId]).Order(StringComparer.Ordinal)]))]))
            .ToList();
    }
}
