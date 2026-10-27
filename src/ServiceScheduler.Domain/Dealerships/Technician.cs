namespace ServiceScheduler.Domain.Dealerships;

public sealed class Technician
{
    private readonly List<TechnicianQualification> _qualifications = [];

    private Technician()
    {
    }

    public Technician(Guid id, Guid dealershipId, string fullName, bool isActive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        Id = id;
        DealershipId = dealershipId;
        FullName = fullName;
        IsActive = isActive;
    }

    public Guid Id { get; private set; }

    public Guid DealershipId { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    /// <summary>Inactive technicians (for example, on leave) are never allocated.</summary>
    public bool IsActive { get; private set; }

    public IReadOnlyCollection<TechnicianQualification> Qualifications => _qualifications;

    public bool IsQualifiedFor(Guid serviceTypeId) => _qualifications.Any(q => q.ServiceTypeId == serviceTypeId);

    public void AddQualification(Guid serviceTypeId)
    {
        if (!IsQualifiedFor(serviceTypeId))
        {
            _qualifications.Add(new TechnicianQualification(Id, serviceTypeId));
        }
    }
}

/// <summary>States that a technician is certified to perform a service type.</summary>
public sealed class TechnicianQualification
{
    private TechnicianQualification()
    {
    }

    public TechnicianQualification(Guid technicianId, Guid serviceTypeId)
    {
        TechnicianId = technicianId;
        ServiceTypeId = serviceTypeId;
    }

    public Guid TechnicianId { get; private set; }

    public Guid ServiceTypeId { get; private set; }
}
