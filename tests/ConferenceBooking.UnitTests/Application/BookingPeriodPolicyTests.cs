using ConferenceBooking.Application.Bookings;
using ConferenceBooking.Domain.Bookings;
using ConferenceBooking.Domain.Common;

namespace ConferenceBooking.UnitTests.Application;

public sealed class BookingPeriodPolicyTests
{
    private static readonly DateTime Now = new(2024, 9, 1, 9, 0, 0);
    private static readonly DateOnly Tomorrow = new(2024, 9, 2);

    private readonly BookingPeriodPolicy _policy = new(
        TestData.Policy(),
        TestData.BookingPolicy(),
        new FixedDateTimeProvider(Now));

    [Fact]
    public void EnsureBookable_WithinWorkingHours_Passes()
    {
        var period = BookingPeriod.Create(Tomorrow, new TimeOnly(10, 0), new TimeOnly(14, 0));

        _policy.EnsureBookable(period);
    }

    [Fact]
    public void EnsureBookable_BeforeOpening_Throws()
    {
        var period = BookingPeriod.Create(Tomorrow, new TimeOnly(5, 0), new TimeOnly(7, 0));

        var exception = Assert.Throws<DomainException>(() => _policy.EnsureBookable(period));

        Assert.Equal("outside_working_hours", exception.Code);
    }

    [Fact]
    public void EnsureBookable_AfterClosing_Throws()
    {
        var period = BookingPeriod.Create(Tomorrow, new TimeOnly(22, 0), new TimeOnly(23, 30));

        var exception = Assert.Throws<DomainException>(() => _policy.EnsureBookable(period));

        Assert.Equal("outside_working_hours", exception.Code);
    }

    [Fact]
    public void EnsureBookable_UnalignedToSlotGrid_Throws()
    {
        var period = BookingPeriod.Create(Tomorrow, new TimeOnly(10, 7), new TimeOnly(12, 7));

        var exception = Assert.Throws<DomainException>(() => _policy.EnsureBookable(period));

        Assert.Equal("unaligned_time", exception.Code);
    }

    [Fact]
    public void EnsureBookable_InThePast_Throws()
    {
        var yesterday = DateOnly.FromDateTime(Now).AddDays(-1);
        var period = BookingPeriod.Create(yesterday, new TimeOnly(10, 0), new TimeOnly(12, 0));

        var exception = Assert.Throws<DomainException>(() => _policy.EnsureBookable(period));

        Assert.Equal("booking_in_the_past", exception.Code);
    }

    [Fact]
    public void EnsureBookable_TooFarAhead_Throws()
    {
        var farFuture = DateOnly.FromDateTime(Now).AddDays(400);
        var period = BookingPeriod.Create(farFuture, new TimeOnly(10, 0), new TimeOnly(12, 0));

        var exception = Assert.Throws<DomainException>(() => _policy.EnsureBookable(period));

        Assert.Equal("booking_too_far_ahead", exception.Code);
    }

    [Fact]
    public void EnsureSearchable_PastDate_Allowed()
    {
        // Дивитися розклад на минуле можна — бронювати не можна.
        var yesterday = DateOnly.FromDateTime(Now).AddDays(-1);
        var period = BookingPeriod.Create(yesterday, new TimeOnly(10, 0), new TimeOnly(12, 0));

        _policy.EnsureSearchable(period);
    }
}
