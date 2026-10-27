using System.Net;
using Microsoft.EntityFrameworkCore;
using ServiceScheduler.IntegrationTests.Support;

namespace ServiceScheduler.IntegrationTests;

public sealed class IdempotencyTests : ApiTestBase
{
    [Fact]
    public async Task Retrying_with_the_same_key_returns_the_original_appointment_instead_of_booking_again()
    {
        using var first = await BookAsync(SamFocusOilChange(Monday(9)), "booking-123");
        var original = await ReadAsync(first, HttpStatusCode.Created);

        using var retry = await BookAsync(SamFocusOilChange(Monday(9)), "booking-123");
        var replayed = await ReadAsync(retry, HttpStatusCode.Created);

        Assert.False(first.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal("true", Assert.Single(retry.Headers.GetValues("Idempotent-Replayed")));
        Assert.Equal(original.ToString(), replayed.ToString());
        Assert.Equal(first.Headers.Location, retry.Headers.Location);
        Assert.Equal(1, await Factory.QueryAsync(db => db.Appointments.CountAsync(Ct)));
    }

    [Fact]
    public async Task The_same_moment_written_with_another_offset_counts_as_the_same_request()
    {
        var original = await ReadAsync(await BookAsync(SamFocusOilChange(Monday(9)), "offset-key"), HttpStatusCode.Created);

        using var retry = await BookAsync(SamFocusOilChange("2026-11-02T10:00:00+01:00"), "offset-key");

        Assert.Equal(original.Text("id"), (await ReadAsync(retry, HttpStatusCode.Created)).Text("id"));
    }

    [Fact]
    public async Task Reusing_a_key_for_a_different_booking_is_rejected()
    {
        await ReadAsync(await BookAsync(SamFocusOilChange(Monday(9)), "reused-key"), HttpStatusCode.Created);

        using var different = await BookAsync(SamFocusOilChange(Monday(11)), "reused-key");

        await AssertProblemAsync(different, HttpStatusCode.UnprocessableEntity, "idempotency_key_reused");
        Assert.Equal(1, await Factory.QueryAsync(db => db.Appointments.CountAsync(Ct)));
    }

    [Fact]
    public async Task Malformed_keys_are_rejected_before_anything_is_booked()
    {
        using var withSpace = await BookAsync(SamFocusOilChange(Monday(9)), "has space");
        using var tooLong = await BookAsync(SamFocusOilChange(Monday(9)), new string('k', 101));

        await AssertProblemAsync(withSpace, HttpStatusCode.BadRequest, "invalid_idempotency_key");
        await AssertProblemAsync(tooLong, HttpStatusCode.BadRequest, "invalid_idempotency_key");
        Assert.Equal(0, await Factory.QueryAsync(db => db.Appointments.CountAsync(Ct)));
    }
}
