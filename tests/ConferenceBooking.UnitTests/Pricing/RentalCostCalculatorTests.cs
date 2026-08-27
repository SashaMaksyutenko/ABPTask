using ConferenceBooking.Domain.Bookings;
using ConferenceBooking.Domain.Common;
using ConferenceBooking.Domain.Pricing;

namespace ConferenceBooking.UnitTests.Pricing;

/// <summary>
/// Перевірка правил розрахунку вартості з ТЗ.
/// Це найважливіша логіка сервісу: помилка тут — це помилка в грошах клієнта.
/// </summary>
public sealed class RentalCostCalculatorTests
{
    private static readonly DateOnly AnyDate = new(2024, 9, 2);

    private readonly RentalCostCalculator _calculator = TestData.Calculator();

    [Theory]
    // Стандартні години: базова вартість без змін. 2000 × 2 год.
    [InlineData("10:00", "12:00", 4000)]
    // Вечірні години: знижка 20%. 2000 × 0.8 × 2 год.
    [InlineData("18:00", "20:00", 3200)]
    // Ранкові години: знижка 10%. 2000 × 0.9 × 2 год.
    [InlineData("07:00", "09:00", 3600)]
    // Пікові години: націнка 15%. 2000 × 1.15 × 2 год.
    [InlineData("12:00", "14:00", 4600)]
    public void Calculate_WithinSingleBand_AppliesThatBandRate(string start, string end, decimal expected)
    {
        var period = Period(start, end);

        var result = _calculator.Calculate(TestData.RoomAPricePerHour, period, []);

        Assert.Equal(expected, result.RoomCost);
    }

    [Fact]
    public void Calculate_SpanningStandardAndPeak_SplitsCostByBand()
    {
        // Приклад із ТЗ: 10:00–14:00. Перші дві години — стандарт, наступні дві — пік.
        var period = Period("10:00", "14:00");

        var result = _calculator.Calculate(TestData.RoomAPricePerHour, period, []);

        Assert.Equal(2, result.Segments.Count);
        Assert.Equal(4000m, result.Segments[0].Amount);  // 2000 × 1.00 × 2
        Assert.Equal(4600m, result.Segments[1].Amount);  // 2000 × 1.15 × 2
        Assert.Equal(8600m, result.RoomCost);
    }

    [Fact]
    public void Calculate_SpanningMorningAndStandard_SplitsCostByBand()
    {
        var period = Period("08:00", "10:00");

        var result = _calculator.Calculate(TestData.RoomAPricePerHour, period, []);

        Assert.Equal(1800m, result.Segments[0].Amount);  // 2000 × 0.90 × 1
        Assert.Equal(2000m, result.Segments[1].Amount);  // 2000 × 1.00 × 1
        Assert.Equal(3800m, result.RoomCost);
    }

    [Fact]
    public void Calculate_PeakHoursOverlapStandard_PeakWinsByPriority()
    {
        // Пікові години лежать усередині стандартних; перемогти має смуга з вищим пріоритетом.
        var period = Period("11:00", "13:00");

        var result = _calculator.Calculate(TestData.RoomAPricePerHour, period, []);

        Assert.Equal("Стандартні години", result.Segments[0].BandName);
        Assert.Equal("Пікові години", result.Segments[1].BandName);
        Assert.Equal(4300m, result.RoomCost);            // 2000 + 2300
    }

    [Fact]
    public void Calculate_HalfHourSlot_ProratesCost()
    {
        var period = Period("12:00", "12:30");

        var result = _calculator.Calculate(TestData.RoomAPricePerHour, period, []);

        Assert.Equal(1150m, result.RoomCost);            // 2000 × 1.15 × 0.5
    }

    [Fact]
    public void Calculate_FullWorkingDay_CoversEveryBandExactlyOnce()
    {
        // 06:00–23:00 не вміщується в одне бронювання (ліміт 12 год), тож перевіряємо
        // покриття смуг на двох суміжних відрізках.
        var morning = _calculator.Calculate(TestData.RoomAPricePerHour, Period("06:00", "14:00"), []);
        var evening = _calculator.Calculate(TestData.RoomAPricePerHour, Period("14:00", "23:00"), []);

        var bands = morning.Segments.Concat(evening.Segments).Select(s => s.BandName).ToArray();

        Assert.Equal(
            ["Ранкові години", "Стандартні години", "Пікові години", "Стандартні години", "Вечірні години"],
            bands);

        // 06–09: 3 × 0.9 = 2.7 | 09–12: 3 × 1.0 = 3 | 12–14: 2 × 1.15 = 2.3
        // 14–18: 4 × 1.0 = 4   | 18–23: 5 × 0.8 = 4
        Assert.Equal(2000m * (2.7m + 3m + 2.3m), morning.RoomCost);
        Assert.Equal(2000m * (4m + 4m), evening.RoomCost);
    }

    [Fact]
    public void Calculate_WithAmenities_AddsThemAsOneTimeCharges()
    {
        var amenities = new[]
        {
            new AmenityCharge(Guid.NewGuid(), "Проєктор", 500m),
            new AmenityCharge(Guid.NewGuid(), "Wi-Fi", 300m)
        };

        var result = _calculator.Calculate(TestData.RoomAPricePerHour, Period("10:00", "14:00"), amenities);

        Assert.Equal(8600m, result.RoomCost);
        Assert.Equal(800m, result.AmenitiesCost);
        Assert.Equal(9400m, result.Total);
    }

    [Fact]
    public void Calculate_OutsideWorkingHours_Throws()
    {
        // 23:00–06:00 не покрито жодною тарифною смугою.
        var period = BookingPeriod.Create(AnyDate, new TimeOnly(23, 0), new TimeOnly(23, 30));

        var exception = Assert.Throws<DomainException>(
            () => _calculator.Calculate(TestData.RoomAPricePerHour, period, []));

        Assert.Equal("time_outside_working_hours", exception.Code);
    }

    [Fact]
    public void Calculate_SegmentsAlwaysSumToRoomCost()
    {
        var result = _calculator.Calculate(TestData.RoomAPricePerHour, Period("08:30", "18:30"), []);

        Assert.Equal(result.RoomCost, result.Segments.Sum(s => s.Amount));
    }

    private static BookingPeriod Period(string start, string end) =>
        BookingPeriod.Create(AnyDate, TimeOnly.Parse(start), TimeOnly.Parse(end));
}
