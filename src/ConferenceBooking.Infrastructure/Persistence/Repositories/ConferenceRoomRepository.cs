using ConferenceBooking.Domain.Bookings;
using ConferenceBooking.Domain.Common;
using ConferenceBooking.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace ConferenceBooking.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IConferenceRoomRepository"/>
public sealed class ConferenceRoomRepository : IConferenceRoomRepository
{
    private readonly ConferenceBookingDbContext _db;

    public ConferenceRoomRepository(ConferenceBookingDbContext db) => _db = db;

    public Task<ConferenceRoom?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        ActiveRoomsWithAmenities().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ConferenceRoom>> ListAsync(CancellationToken cancellationToken = default) =>
        await ActiveRoomsWithAmenities()
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<bool> NameExistsAsync(
        string name,
        Guid? excludeRoomId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = NameNormalizer.Normalize(name);

        return _db.Rooms
            .Where(r => !r.IsDeleted && r.NormalizedName == normalized)
            .Where(r => excludeRoomId == null || r.Id != excludeRoomId)
            .AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConferenceRoom>> FindAvailableAsync(
        BookingPeriod period,
        int minimumCapacity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(period);

        // Підзапит зайнятих залів виконується в СУБД: вивантажувати всі бронювання в пам'ять,
        // щоб відфільтрувати їх у застосунку, було б непридатним уже на кількох тисячах записів.
        var occupiedRoomIds = _db.Bookings
            .Where(b => b.Status == BookingStatus.Confirmed
                        && b.StartAt < period.End
                        && period.Start < b.EndAt)
            .Select(b => b.RoomId);

        return await ActiveRoomsWithAmenities()
            .Where(r => r.Capacity >= minimumCapacity && !occupiedRoomIds.Contains(r.Id))
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public void Add(ConferenceRoom room) => _db.Rooms.Add(room);

    /// <summary>
    /// Активні зали разом із послугами. Послуги підвантажуються завжди: усі сценарії,
    /// що читають зал, показують і його послуги, тож ліниве завантаження давало б лише
    /// приховані додаткові запити.
    /// </summary>
    private IQueryable<ConferenceRoom> ActiveRoomsWithAmenities() =>
        _db.Rooms
            .Where(r => !r.IsDeleted)
            .Include(r => r.Amenities)
            .ThenInclude(a => a.Amenity);
}
