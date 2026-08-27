using ConferenceBooking.Domain.Common;

namespace ConferenceBooking.Domain.Bookings;

/// <summary>
/// Період бронювання в межах однієї доби — об'єкт-значення.
///
/// Час трактується як локальний час закладу (не UTC): тарифні смуги з ТЗ задані настінним часом,
/// тож «вечірня знижка з 18:00» має означати 18:00 на годиннику в холі, незалежно від
/// часового поясу клієнта чи переходу на літній час.
/// </summary>
public sealed class BookingPeriod : IEquatable<BookingPeriod>
{
    /// <summary>Максимальна тривалість одного бронювання — захист від помилкових вводів і DoS-подібних сценаріїв.</summary>
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(12);

    /// <summary>Мінімальна тривалість одного бронювання.</summary>
    public static readonly TimeSpan MinDuration = TimeSpan.FromMinutes(30);

    /// <summary>Дата бронювання.</summary>
    public DateOnly Date { get; }

    /// <summary>Час початку (включно).</summary>
    public TimeOnly StartTime { get; }

    /// <summary>Час завершення (не включно).</summary>
    public TimeOnly EndTime { get; }

    /// <summary>Початок періоду як момент часу — зручно для запитів до сховища.</summary>
    public DateTime Start => Date.ToDateTime(StartTime);

    /// <summary>Кінець періоду як момент часу.</summary>
    public DateTime End => Date.ToDateTime(EndTime);

    /// <summary>Тривалість бронювання.</summary>
    public TimeSpan Duration => End - Start;

    /// <summary>Тривалість у годинах.</summary>
    public decimal Hours => (decimal)Duration.TotalHours;

    private BookingPeriod(DateOnly date, TimeOnly startTime, TimeOnly endTime)
    {
        Date = date;
        StartTime = startTime;
        EndTime = endTime;
    }

    /// <summary>Створює період за датою та межами часу.</summary>
    public static BookingPeriod Create(DateOnly date, TimeOnly startTime, TimeOnly endTime)
    {
        if (endTime <= startTime)
        {
            throw new DomainException(
                "invalid_period",
                "Час завершення має бути пізнішим за час початку. Бронювання через опівніч не підтримується.");
        }

        var duration = endTime - startTime;

        if (duration < MinDuration)
        {
            throw new DomainException(
                "period_too_short",
                $"Мінімальна тривалість бронювання — {MinDuration.TotalMinutes:0} хвилин.");
        }

        if (duration > MaxDuration)
        {
            throw new DomainException(
                "period_too_long",
                $"Максимальна тривалість бронювання — {MaxDuration.TotalHours:0} годин.");
        }

        return new BookingPeriod(date, startTime, endTime);
    }

    /// <summary>Створює період за датою, початком і тривалістю (варіант вхідних даних із ТЗ).</summary>
    public static BookingPeriod FromDuration(DateOnly date, TimeOnly startTime, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new DomainException("invalid_period", "Тривалість бронювання має бути додатною.");
        }

        // Перевіряємо вихід за межі доби до додавання, щоб не отримати мовчазне «перетікання» на завтра.
        if (startTime.ToTimeSpan() + duration > TimeSpan.FromDays(1))
        {
            throw new DomainException(
                "invalid_period",
                "Бронювання не може виходити за межі доби.");
        }

        return Create(date, startTime, startTime.Add(duration));
    }

    /// <summary>
    /// Чи перетинається цей період з іншим. Межі напіввідкриті: бронювання 10:00–12:00 і 12:00–14:00
    /// не конфліктують, бо зал звільняється рівно о 12:00.
    /// </summary>
    public bool OverlapsWith(BookingPeriod other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Start < other.End && other.Start < End;
    }

    public bool Equals(BookingPeriod? other) =>
        other is not null && Date == other.Date && StartTime == other.StartTime && EndTime == other.EndTime;

    public override bool Equals(object? obj) => Equals(obj as BookingPeriod);

    public override int GetHashCode() => HashCode.Combine(Date, StartTime, EndTime);

    public override string ToString() => $"{Date:dd.MM.yyyy} {StartTime:HH\\:mm}–{EndTime:HH\\:mm}";
}
