using ConferenceBooking.Application.Common;
using ConferenceBooking.Application.Configuration;
using ConferenceBooking.Domain.Pricing;

namespace ConferenceBooking.UnitTests;

/// <summary>
/// Спільні дані для тестів: тарифна сітка з ТЗ і фіксований годинник.
/// Тарифи задаються тим самим шляхом, що й у застосунку (через конфігурацію),
/// тож тести перевіряють і коректність самої конфігурації.
/// </summary>
internal static class TestData
{
    /// <summary>Базова вартість «Залу А» з ТЗ.</summary>
    public const decimal RoomAPricePerHour = 2000m;

    /// <summary>Тарифна сітка з технічного завдання.</summary>
    public static PricingOptions PricingOptionsFromSpec() => new()
    {
        Bands =
        [
            new PricingBandOptions { Name = "Ранкові години", Start = "06:00", End = "09:00", Multiplier = 0.90m, Priority = 50 },
            new PricingBandOptions { Name = "Стандартні години", Start = "09:00", End = "18:00", Multiplier = 1.00m, Priority = 10 },
            new PricingBandOptions { Name = "Пікові години", Start = "12:00", End = "14:00", Multiplier = 1.15m, Priority = 100 },
            new PricingBandOptions { Name = "Вечірні години", Start = "18:00", End = "23:00", Multiplier = 0.80m, Priority = 50 }
        ]
    };

    public static PricingPolicy Policy() => PricingOptionsFromSpec().ToPolicy();

    public static RentalCostCalculator Calculator() => new(Policy());

    public static BookingPolicyOptions BookingPolicy() => new()
    {
        MaxAdvanceDays = 365,
        SlotSizeMinutes = 30,
        MaxAttendees = 10_000
    };
}

/// <summary>Годинник із наперед заданим часом — робить тести незалежними від дати запуску.</summary>
internal sealed class FixedDateTimeProvider : IDateTimeProvider
{
    public FixedDateTimeProvider(DateTime localNow) => LocalNow = localNow;

    public DateTime LocalNow { get; }
}
