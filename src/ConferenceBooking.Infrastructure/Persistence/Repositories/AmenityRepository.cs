using ConferenceBooking.Domain.Common;
using ConferenceBooking.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace ConferenceBooking.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IAmenityRepository"/>
public sealed class AmenityRepository : IAmenityRepository
{
    private readonly ConferenceBookingDbContext _db;

    public AmenityRepository(ConferenceBookingDbContext db) => _db = db;

    /// <summary>Пошук за нормалізованою назвою — нечутливий до регістру для будь-якої мови.</summary>
    public Task<Amenity?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = NameNormalizer.Normalize(name);
        return _db.Amenities.FirstOrDefaultAsync(a => a.NormalizedName == normalized, cancellationToken);
    }

    public void Add(Amenity amenity) => _db.Amenities.Add(amenity);
}
