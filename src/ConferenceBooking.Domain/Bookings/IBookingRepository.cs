namespace ConferenceBooking.Domain.Bookings;

/// <summary>Доступ до бронювань.</summary>
public interface IBookingRepository
{
    /// <summary>Бронювання разом із замовленими послугами.</summary>
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Чи є в залі підтверджене бронювання, що перетинається з переданим періодом.
    /// Перевірка виконується в СУБД, а не в пам'яті, — інакше на великих обсягах вона б не масштабувалася.
    /// </summary>
    Task<bool> HasOverlapAsync(
        Guid roomId,
        BookingPeriod period,
        CancellationToken cancellationToken = default);

    /// <summary>Бронювання за діапазоном дат — джерело даних для звітів.</summary>
    Task<IReadOnlyList<Booking>> ListByDateRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Чи має зал підтверджені бронювання починаючи з указаної дати.
    /// Використовується, щоб не видалити зал, на який уже продані майбутні брони.
    /// </summary>
    Task<bool> HasConfirmedBookingsFromAsync(
        Guid roomId,
        DateOnly from,
        CancellationToken cancellationToken = default);

    void Add(Booking booking);
}
