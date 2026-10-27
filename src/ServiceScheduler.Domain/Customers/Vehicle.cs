namespace ServiceScheduler.Domain.Customers;

public sealed class Vehicle
{
    public const int VinLength = 17;

    private Vehicle()
    {
    }

    public Vehicle(Guid id, Guid customerId, string vin, string registration, string make, string model, int modelYear)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vin);
        ArgumentException.ThrowIfNullOrWhiteSpace(registration);
        ArgumentException.ThrowIfNullOrWhiteSpace(make);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        if (vin.Length != VinLength)
        {
            throw new ArgumentException($"A VIN has exactly {VinLength} characters.", nameof(vin));
        }

        Id = id;
        CustomerId = customerId;
        Vin = vin;
        Registration = registration;
        Make = make;
        Model = model;
        ModelYear = modelYear;
    }

    public Guid Id { get; private set; }

    /// <summary>The registered keeper; only this customer may book the vehicle in.</summary>
    public Guid CustomerId { get; private set; }

    public string Vin { get; private set; } = string.Empty;

    public string Registration { get; private set; } = string.Empty;

    public string Make { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;

    public int ModelYear { get; private set; }
}
