using ConferenceBooking.Domain.Bookings;
using ConferenceBooking.Domain.Common;

namespace ConferenceBooking.Domain.Pricing;

/// <summary>Розрахунок вартості оренди залу за період з урахуванням тарифних смуг і послуг.</summary>
public interface IRentalCostCalculator
{
    RentalCostBreakdown Calculate(
        decimal basePricePerHour,
        BookingPeriod period,
        IReadOnlyCollection<AmenityCharge> amenities);
}

/// <summary>
/// Реалізація розрахунку вартості.
///
/// Алгоритм: період бронювання ріжеться на ділянки по межах тарифних смуг, кожна ділянка
/// оцінюється за власною ставкою, після чого додаються разові платежі за послуги.
/// Такий підхід коректно обробляє бронювання, що перетинає кілька смуг
/// (наприклад, 10:00–14:00 = 2 год за стандартом + 2 год за піковою націнкою).
/// </summary>
public sealed class RentalCostCalculator : IRentalCostCalculator
{
    private const int MoneyDecimals = 2;

    private readonly PricingPolicy _policy;

    public RentalCostCalculator(PricingPolicy policy) =>
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));

    public RentalCostBreakdown Calculate(
        decimal basePricePerHour,
        BookingPeriod period,
        IReadOnlyCollection<AmenityCharge> amenities)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(amenities);
        ArgumentOutOfRangeException.ThrowIfNegative(basePricePerHour);

        var segments = BuildSegments(basePricePerHour, period);
        var roundedRoomCost = Round(segments.Sum(s => s.Amount));

        var amenityCharges = amenities.ToArray();
        var amenitiesCost = Round(amenityCharges.Sum(a => a.Price));

        return new RentalCostBreakdown(
            segments,
            roundedRoomCost,
            amenityCharges,
            amenitiesCost,
            Round(roundedRoomCost + amenitiesCost));
    }

    /// <summary>Ріже період на ділянки з однаковою ставкою і оцінює кожну з них.</summary>
    private IReadOnlyList<RentalCostSegment> BuildSegments(decimal basePricePerHour, BookingPeriod period)
    {
        var cutPoints = CutPointsWithin(period);
        var segments = new List<RentalCostSegment>(cutPoints.Count - 1);

        for (var i = 0; i < cutPoints.Count - 1; i++)
        {
            var from = cutPoints[i];
            var to = cutPoints[i + 1];

            var band = _policy.FindBand(from)
                ?? throw new DomainException(
                    "time_outside_working_hours",
                    $"Заклад зачинено о {from:HH\\:mm}. Робочі години: " +
                    $"{_policy.OpeningTime:HH\\:mm}–{_policy.ClosingTime:HH\\:mm}.");

            var hours = (decimal)(to - from).TotalHours;
            var amount = Round(basePricePerHour * band.Multiplier * hours);

            segments.Add(new RentalCostSegment(band.Name, from, to, hours, band.Multiplier, amount));
        }

        return segments;
    }

    /// <summary>
    /// Точки розрізу: початок і кінець бронювання плюс усі межі тарифних смуг, що потрапили всередину.
    /// </summary>
    private List<TimeOnly> CutPointsWithin(BookingPeriod period)
    {
        var points = new SortedSet<TimeOnly> { period.StartTime, period.EndTime };

        foreach (var boundary in _policy.BandBoundaries())
        {
            if (boundary > period.StartTime && boundary < period.EndTime)
            {
                points.Add(boundary);
            }
        }

        return points.ToList();
    }

    /// <summary>
    /// Округлення грошових сум. AwayFromZero замість банківського округлення за замовчуванням —
    /// саме так рахують гроші в бухгалтерії, і саме такий результат очікує клієнт.
    /// </summary>
    private static decimal Round(decimal value) =>
        Math.Round(value, MoneyDecimals, MidpointRounding.AwayFromZero);
}
