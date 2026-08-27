using ConferenceBooking.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConferenceBooking.Infrastructure.Persistence;

/// <summary>
/// Початкове наповнення бази даними з технічного завдання.
/// Операція ідемпотентна: повторний запуск застосунку не створює дублікатів.
/// </summary>
public sealed class DatabaseSeeder
{
    private static readonly (string Name, decimal Price)[] CatalogAmenities =
    [
        ("Проєктор", 500m),
        ("Wi-Fi", 300m),
        ("Звук", 700m)
    ];

    private static readonly (string Name, int Capacity, decimal PricePerHour)[] Rooms =
    [
        ("Зал А", 50, 2000m),
        ("Зал B", 100, 3500m),
        ("Зал C", 30, 1500m)
    ];

    private readonly ConferenceBookingDbContext _db;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(ConferenceBookingDbContext db, ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Застосовує міграції та створює початкові дані, якщо їх ще немає.</summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await _db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        var amenities = await EnsureAmenitiesAsync(cancellationToken).ConfigureAwait(false);
        await EnsureRoomsAsync(amenities, cancellationToken).ConfigureAwait(false);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Dictionary<string, Amenity>> EnsureAmenitiesAsync(CancellationToken cancellationToken)
    {
        var existing = await _db.Amenities
            .ToDictionaryAsync(a => a.Name, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        foreach (var (name, price) in CatalogAmenities)
        {
            if (existing.ContainsKey(name))
            {
                continue;
            }

            var amenity = new Amenity(name, price);
            _db.Amenities.Add(amenity);
            existing[name] = amenity;

            _logger.LogInformation("Створено послугу «{Amenity}» ({Price} грн)", name, price);
        }

        return existing;
    }

    private async Task EnsureRoomsAsync(
        IReadOnlyDictionary<string, Amenity> amenities,
        CancellationToken cancellationToken)
    {
        var existingNames = await _db.Rooms
            .Select(r => r.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var known = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        foreach (var (name, capacity, pricePerHour) in Rooms)
        {
            if (known.Contains(name))
            {
                continue;
            }

            var room = new ConferenceRoom(name, capacity, pricePerHour);

            // Усі три зали з ТЗ пропонують повний перелік послуг закладу.
            room.ReplaceAmenities(CatalogAmenities.Select(a => (amenities[a.Name], a.Price)));

            _db.Rooms.Add(room);

            _logger.LogInformation(
                "Створено зал «{Room}»: {Capacity} осіб, {Price} грн/год",
                name, capacity, pricePerHour);
        }
    }
}
