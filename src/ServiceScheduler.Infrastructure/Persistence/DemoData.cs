using ServiceScheduler.Domain.Catalog;
using ServiceScheduler.Domain.Customers;
using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.Infrastructure.Persistence;

/// <summary>
/// Deterministic, fictional reference data for local runs, demos and tests. Ids use readable prefixes:
/// de = dealership, 5e = service type, ba = bay, 7e = technician, c0 = customer, ca = car.
/// </summary>
public static class DemoData
{
    public static IReadOnlyList<object> Create()
    {
        var oilChange = new ServiceType(ServiceTypes.OilChange, "OIL", "Oil and filter change", 60);
        var mot = new ServiceType(ServiceTypes.Mot, "MOT", "MOT test", 60);
        var fullService = new ServiceType(ServiceTypes.FullService, "FULL", "Full service", 120);
        var brakes = new ServiceType(ServiceTypes.Brakes, "BRAKES", "Brake pad replacement", 90);
        var evHealthCheck = new ServiceType(ServiceTypes.EvHealthCheck, "EV_HEALTH", "EV battery health check", 30);
        var tyres = new ServiceType(ServiceTypes.Tyres, "TYRES", "Tyre fitting (set of four)", 45);

        var manchester = new Dealership(
            Dealerships.Manchester, "Northgate Motors Manchester", "Europe/London", new TimeOnly(8, 0), new TimeOnly(18, 0));
        var pune = new Dealership(
            Dealerships.Pune, "Deccan Motors Pune", "Asia/Kolkata", new TimeOnly(9, 0), new TimeOnly(18, 0));

        return
        [
            oilChange, mot, fullService, brakes, evHealthCheck, tyres,
            manchester, pune,
            new ServiceBay(ServiceBays.ManchesterBay1, manchester.Id, "Bay 1"),
            new ServiceBay(ServiceBays.ManchesterBay2, manchester.Id, "Bay 2"),
            new ServiceBay(ServiceBays.ManchesterBay3, manchester.Id, "Bay 3", isActive: false),
            new ServiceBay(ServiceBays.PuneBayA, pune.Id, "Bay A"),
            CreateTechnician(Technicians.AliceMorgan, manchester, "Alice Morgan", oilChange, mot, fullService, brakes),
            CreateTechnician(Technicians.BenCarter, manchester, "Ben Carter", oilChange, tyres, brakes),
            CreateTechnician(Technicians.ChloeDavies, manchester, "Chloe Davies", evHealthCheck, oilChange, fullService),
            CreateTechnician(Technicians.PriyaNair, pune, "Priya Nair", oilChange, tyres, fullService),
            new Customer(Customers.SamTaylor, "Sam Taylor", "sam.taylor@example.com"),
            new Customer(Customers.EmmaWilson, "Emma Wilson", "emma.wilson@example.com"),
            new Customer(Customers.ArjunMehta, "Arjun Mehta", "arjun.mehta@example.com"),
            new Vehicle(Vehicles.SamFocus, Customers.SamTaylor, "WF0XXXGCDXKA00001", "MA19 KLP", "Ford", "Focus", 2019),
            new Vehicle(Vehicles.SamLeaf, Customers.SamTaylor, "SJNFAAZE0U0000002", "MA22 EVL", "Nissan", "Leaf", 2022),
            new Vehicle(Vehicles.EmmaGolf, Customers.EmmaWilson, "WVWZZZ1KZLW000003", "ML20 WGF", "Volkswagen", "Golf", 2020),
            new Vehicle(Vehicles.EmmaModel3, Customers.EmmaWilson, "5YJ3E7EB0PF000005", "MM23 TSL", "Tesla", "Model 3", 2023),
            new Vehicle(Vehicles.ArjunCreta, Customers.ArjunMehta, "MALC381CLPM000004", "MH12 AB 1234", "Hyundai", "Creta", 2023),
        ];
    }

    private static Technician CreateTechnician(Guid id, Dealership dealership, string name, params ServiceType[] qualifications)
    {
        var technician = new Technician(id, dealership.Id, name);
        foreach (var serviceType in qualifications)
        {
            technician.AddQualification(serviceType.Id);
        }

        return technician;
    }

    public static class Dealerships
    {
        /// <summary>Europe/London, 08:00-18:00, two active bays.</summary>
        public static readonly Guid Manchester = new("de000000-0000-0000-0000-000000000001");

        /// <summary>Asia/Kolkata (UTC+05:30), 09:00-18:00, one bay and one technician.</summary>
        public static readonly Guid Pune = new("de000000-0000-0000-0000-000000000002");
    }

    public static class ServiceTypes
    {
        public static readonly Guid OilChange = new("5e000000-0000-0000-0000-000000000001");
        public static readonly Guid Mot = new("5e000000-0000-0000-0000-000000000002");
        public static readonly Guid FullService = new("5e000000-0000-0000-0000-000000000003");
        public static readonly Guid Brakes = new("5e000000-0000-0000-0000-000000000004");
        public static readonly Guid EvHealthCheck = new("5e000000-0000-0000-0000-000000000005");
        public static readonly Guid Tyres = new("5e000000-0000-0000-0000-000000000006");
    }

    public static class ServiceBays
    {
        public static readonly Guid ManchesterBay1 = new("ba000000-0000-0000-0000-000000000001");
        public static readonly Guid ManchesterBay2 = new("ba000000-0000-0000-0000-000000000002");

        /// <summary>Inactive (under maintenance), so it is never allocated.</summary>
        public static readonly Guid ManchesterBay3 = new("ba000000-0000-0000-0000-000000000003");

        public static readonly Guid PuneBayA = new("ba000000-0000-0000-0000-000000000011");
    }

    public static class Technicians
    {
        /// <summary>The only Manchester technician qualified for MOT.</summary>
        public static readonly Guid AliceMorgan = new("7e000000-0000-0000-0000-000000000001");
        public static readonly Guid BenCarter = new("7e000000-0000-0000-0000-000000000002");

        /// <summary>The only technician qualified for the EV battery health check.</summary>
        public static readonly Guid ChloeDavies = new("7e000000-0000-0000-0000-000000000003");
        public static readonly Guid PriyaNair = new("7e000000-0000-0000-0000-000000000011");
    }

    public static class Customers
    {
        public static readonly Guid SamTaylor = new("c0000000-0000-0000-0000-000000000001");
        public static readonly Guid EmmaWilson = new("c0000000-0000-0000-0000-000000000002");
        public static readonly Guid ArjunMehta = new("c0000000-0000-0000-0000-000000000003");
    }

    public static class Vehicles
    {
        public static readonly Guid SamFocus = new("ca000000-0000-0000-0000-000000000001");
        public static readonly Guid SamLeaf = new("ca000000-0000-0000-0000-000000000002");
        public static readonly Guid EmmaGolf = new("ca000000-0000-0000-0000-000000000003");
        public static readonly Guid ArjunCreta = new("ca000000-0000-0000-0000-000000000004");
        public static readonly Guid EmmaModel3 = new("ca000000-0000-0000-0000-000000000005");
    }
}
