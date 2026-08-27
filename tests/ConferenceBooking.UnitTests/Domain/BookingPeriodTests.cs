using ConferenceBooking.Domain.Bookings;
using ConferenceBooking.Domain.Common;

namespace ConferenceBooking.UnitTests.Domain;

public sealed class BookingPeriodTests
{
    private static readonly DateOnly Date = new(2024, 9, 2);

    [Fact]
    public void Create_EndBeforeStart_Throws()
    {
        var exception = Assert.Throws<DomainException>(
            () => BookingPeriod.Create(Date, new TimeOnly(14, 0), new TimeOnly(10, 0)));

        Assert.Equal("invalid_period", exception.Code);
    }

    [Fact]
    public void Create_ShorterThanMinimum_Throws()
    {
        var exception = Assert.Throws<DomainException>(
            () => BookingPeriod.Create(Date, new TimeOnly(10, 0), new TimeOnly(10, 15)));

        Assert.Equal("period_too_short", exception.Code);
    }

    [Fact]
    public void Create_LongerThanMaximum_Throws()
    {
        var exception = Assert.Throws<DomainException>(
            () => BookingPeriod.Create(Date, new TimeOnly(6, 0), new TimeOnly(23, 0)));

        Assert.Equal("period_too_long", exception.Code);
    }

    [Fact]
    public void FromDuration_CrossingMidnight_Throws()
    {
        // Тихе «перетікання» на наступну добу зіпсувало б і розрахунок, і перевірку зайнятості.
        var exception = Assert.Throws<DomainException>(
            () => BookingPeriod.FromDuration(Date, new TimeOnly(23, 0), TimeSpan.FromHours(2)));

        Assert.Equal("invalid_period", exception.Code);
    }

    [Fact]
    public void FromDuration_BuildsExpectedEndTime()
    {
        var period = BookingPeriod.FromDuration(Date, new TimeOnly(10, 0), TimeSpan.FromMinutes(240));

        Assert.Equal(new TimeOnly(14, 0), period.EndTime);
        Assert.Equal(4m, period.Hours);
    }

    [Theory]
    // Дотик межами конфліктом не є: зал звільняється рівно о 12:00.
    [InlineData("10:00", "12:00", "12:00", "14:00", false)]
    [InlineData("10:00", "12:00", "11:00", "13:00", true)]
    [InlineData("10:00", "14:00", "11:00", "12:00", true)]
    [InlineData("10:00", "12:00", "14:00", "16:00", false)]
    public void OverlapsWith_DetectsIntersections(
        string firstStart, string firstEnd, string secondStart, string secondEnd, bool expected)
    {
        var first = BookingPeriod.Create(Date, TimeOnly.Parse(firstStart), TimeOnly.Parse(firstEnd));
        var second = BookingPeriod.Create(Date, TimeOnly.Parse(secondStart), TimeOnly.Parse(secondEnd));

        Assert.Equal(expected, first.OverlapsWith(second));
        Assert.Equal(expected, second.OverlapsWith(first));
    }

    [Fact]
    public void OverlapsWith_DifferentDates_NeverConflicts()
    {
        var first = BookingPeriod.Create(Date, new TimeOnly(10, 0), new TimeOnly(14, 0));
        var second = BookingPeriod.Create(Date.AddDays(1), new TimeOnly(10, 0), new TimeOnly(14, 0));

        Assert.False(first.OverlapsWith(second));
    }
}
