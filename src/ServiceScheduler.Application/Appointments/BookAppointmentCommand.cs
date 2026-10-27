using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ServiceScheduler.Application.Appointments;

public sealed record BookAppointmentCommand(
    Guid DealershipId,
    Guid CustomerId,
    Guid VehicleId,
    Guid ServiceTypeId,
    DateTimeOffset StartTime,
    string? Notes,
    string? IdempotencyKey);

internal static class RequestFingerprint
{
    /// <summary>
    /// SHA-256 over the normalised request. The start is reduced to its UTC instant, so the same moment
    /// written with different offsets is the same request.
    /// </summary>
    public static string Compute(BookAppointmentCommand command)
    {
        var canonical = string.Join(
            '\n',
            command.DealershipId.ToString("N"),
            command.CustomerId.ToString("N"),
            command.VehicleId.ToString("N"),
            command.ServiceTypeId.ToString("N"),
            command.StartTime.UtcTicks.ToString(CultureInfo.InvariantCulture),
            command.Notes ?? string.Empty);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
