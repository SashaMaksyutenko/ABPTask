using ConferenceBooking.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace ConferenceBooking.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IBookingRepository"/>
public sealed class BookingRepository : IBookingRepository
{
    private readonly ConferenceBookingDbContext _db;

    public BookingRepository(ConferenceBookingDbContext db) => _db = db;

    public Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Bookings
            .Include(b => b.Amenities)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public Task<bool> HasOverlapAsync(
        Guid roomId,
        BookingPeriod period,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(period);

        // Межі напіввідкриті: бронювання 10:00–12:00 не конфліктує з 12:00–14:00.
        return _db.Bookings.AnyAsync(
            b => b.RoomId == roomId
                 && b.Status == BookingStatus.Confirmed
                 && b.StartAt < period.End
                 && period.Start < b.EndAt,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> ListByDateRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default) =>
        await ReadOnlyQuery()
            .Where(b => b.Date >= from && b.Date <= to)
            .OrderBy(b => b.StartAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<bool> HasConfirmedBookingsFromAsync(
        Guid roomId,
        DateOnly from,
        CancellationToken cancellationToken = default) =>
        _db.Bookings.AnyAsync(
            b => b.RoomId == roomId && b.Status == BookingStatus.Confirmed && b.Date >= from,
            cancellationToken);

    public void Add(Booking booking) => _db.Bookings.Add(booking);

    /// <summary>
    /// Запит для звітів. AsNoTracking: звіти лише читають дані, і тримати тисячі
    /// бронювань у трекері змін — марна витрата пам'яті та часу на кожному SaveChanges.
    /// </summary>
    private IQueryable<Booking> ReadOnlyQuery() =>
        _db.Bookings
            .AsNoTracking()
            .Include(b => b.Amenities);
}
