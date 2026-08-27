using ConferenceBooking.Application.Common;
using ConferenceBooking.Application.Configuration;
using ConferenceBooking.Domain.Bookings;
using ConferenceBooking.Domain.Common;
using ConferenceBooking.Domain.Pricing;

namespace ConferenceBooking.Application.Bookings;

/// <summary>
/// Перевіряє, чи взагалі можна прийняти бронювання на вказаний період.
///
/// Правила зібрані в один клас навмисно: інакше вони розповзаються між контролером,
/// валідатором і сервісом, і рано чи пізно якийсь шлях у коді почне їх обходити.
/// </summary>
public sealed class BookingPeriodPolicy
{
    private readonly PricingPolicy _pricingPolicy;
    private readonly BookingPolicyOptions _options;
    private readonly IDateTimeProvider _clock;

    public BookingPeriodPolicy(
        PricingPolicy pricingPolicy,
        BookingPolicyOptions options,
        IDateTimeProvider clock)
    {
        _pricingPolicy = pricingPolicy;
        _options = options;
        _clock = clock;
    }

    /// <summary>Перевіряє період бронювання; кидає <see cref="DomainException"/> при порушенні правил.</summary>
    public void EnsureBookable(BookingPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);

        EnsureWithinWorkingHours(period);
        EnsureAlignedToSlotGrid(period);
        EnsureNotInThePast(period);
        EnsureNotTooFarAhead(period);
    }

    /// <summary>
    /// Перевіряє проміжок пошуку вільних залів. Обмеження м'якші, ніж для бронювання:
    /// дивитися розклад на минуле корисно, а бронювати — ні.
    /// </summary>
    public void EnsureSearchable(BookingPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);

        EnsureWithinWorkingHours(period);
        EnsureNotTooFarAhead(period);
    }

    private void EnsureWithinWorkingHours(BookingPeriod period)
    {
        if (period.StartTime < _pricingPolicy.OpeningTime || period.EndTime > _pricingPolicy.ClosingTime)
        {
            throw new DomainException(
                "outside_working_hours",
                $"Заклад працює з {_pricingPolicy.OpeningTime:HH\\:mm} до {_pricingPolicy.ClosingTime:HH\\:mm}. " +
                $"Запитаний проміжок {period.StartTime:HH\\:mm}–{period.EndTime:HH\\:mm} виходить за ці межі.");
        }
    }

    private void EnsureAlignedToSlotGrid(BookingPeriod period)
    {
        var slot = _options.SlotSizeMinutes;
        if (slot <= 0)
        {
            return;
        }

        if (MinutesOfDay(period.StartTime) % slot != 0 || MinutesOfDay(period.EndTime) % slot != 0)
        {
            throw new DomainException(
                "unaligned_time",
                $"Час бронювання має бути кратним {slot} хвилинам.");
        }
    }

    private void EnsureNotInThePast(BookingPeriod period)
    {
        if (period.Start <= _clock.LocalNow)
        {
            throw new DomainException(
                "booking_in_the_past",
                "Не можна забронювати зал на час, що вже минув.");
        }
    }

    private void EnsureNotTooFarAhead(BookingPeriod period)
    {
        var lastAllowedDate = _clock.Today.AddDays(_options.MaxAdvanceDays);
        if (period.Date > lastAllowedDate)
        {
            throw new DomainException(
                "booking_too_far_ahead",
                $"Бронювання приймаються не більше ніж на {_options.MaxAdvanceDays} днів наперед " +
                $"(до {lastAllowedDate:dd.MM.yyyy}).");
        }
    }

    private static int MinutesOfDay(TimeOnly time) => time.Hour * 60 + time.Minute;
}
