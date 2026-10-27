using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ServiceScheduler.Application.Abstractions;
using ServiceScheduler.Application.Catalog;
using ServiceScheduler.Domain.Common;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Application.Availability;

/// <param name="Date">Local date at the dealership; defaults to the next local day.</param>
public sealed record GetAvailabilityQuery(Guid DealershipId, Guid ServiceTypeId, DateOnly? Date);

public sealed record AvailableSlotDto(DateTimeOffset Start, DateTimeOffset End, int FreeServiceBays, int FreeTechnicians);

public sealed record AvailabilityDto(
    Guid DealershipId,
    string TimeZone,
    DateOnly Date,
    ServiceTypeDto ServiceType,
    IReadOnlyList<AvailableSlotDto> Slots);

/// <summary>
/// Lists start times at which a bay and a qualified technician are both free for the whole service.
/// It applies the same opening-hours, horizon and slot rules as booking; the vehicle is checked at booking time.
/// </summary>
public sealed class GetAvailabilityHandler(
    ISchedulerDbContext db, TimeProvider clock, IOptions<SchedulingOptions> options)
{
    public async Task<Result<AvailabilityDto>> HandleAsync(GetAvailabilityQuery query, CancellationToken cancellationToken)
    {
        var dealership = await db.Dealerships.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == query.DealershipId, cancellationToken);
        if (dealership is null)
        {
            return SchedulingErrors.DealershipNotFound;
        }

        var serviceType = await db.ServiceTypes.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == query.ServiceTypeId, cancellationToken);
        if (serviceType is null)
        {
            return SchedulingErrors.UnknownServiceType;
        }

        var now = clock.GetUtcNow();
        var horizon = now.AddDays(options.Value.BookingHorizonDays);
        var date = query.Date ?? dealership.LocalDateOf(now).AddDays(1);
        var candidates = dealership.CandidateRanges(date, serviceType.Duration)
            .Where(r => r.StartUtc > now && r.StartUtc <= horizon)
            .ToList();

        var bayIds = await db.ServiceBays.AsNoTracking()
            .Where(b => b.DealershipId == dealership.Id && b.IsActive)
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);
        var technicianIds = await db.Technicians.AsNoTracking()
            .Where(t => t.DealershipId == dealership.Id
                && t.IsActive
                && t.Qualifications.Any(q => q.ServiceTypeId == serviceType.Id))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var slots = new List<AvailableSlotDto>();
        if (candidates.Count > 0 && bayIds.Count > 0 && technicianIds.Count > 0)
        {
            var occupancy = await db.LoadOccupancyAsync(
                [.. bayIds, .. technicianIds], candidates[0].StartUtc, candidates[^1].EndUtc, cancellationToken);

            foreach (var range in candidates)
            {
                var freeBays = bayIds.Count(id => occupancy.IsFree(ResourceKind.ServiceBay, id, range));
                var freeTechnicians = technicianIds.Count(id => occupancy.IsFree(ResourceKind.Technician, id, range));
                if (freeBays > 0 && freeTechnicians > 0)
                {
                    slots.Add(new AvailableSlotDto(
                        dealership.ToLocalTime(range.StartUtc), dealership.ToLocalTime(range.EndUtc), freeBays, freeTechnicians));
                }
            }
        }

        return new AvailabilityDto(dealership.Id, dealership.TimeZoneId, date, ServiceTypeDto.From(serviceType), slots);
    }
}
