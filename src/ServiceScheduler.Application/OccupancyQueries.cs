using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Application.Abstractions;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Application;

internal static class OccupancyQueries
{
    /// <summary>Loads every reservation for the given resources in [fromUtc, toUtc) with a single indexed query.</summary>
    public static async Task<ResourceOccupancy> LoadOccupancyAsync(
        this ISchedulerDbContext db,
        List<Guid> resourceIds,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var reserved = await db.SlotReservations.AsNoTracking()
            .Where(r => resourceIds.Contains(r.ResourceId) && r.SlotStartUtc >= fromUtc && r.SlotStartUtc < toUtc)
            .Select(r => new ResourceSlot(r.ResourceKind, r.ResourceId, r.SlotStartUtc))
            .ToListAsync(cancellationToken);

        return new ResourceOccupancy(reserved);
    }
}
