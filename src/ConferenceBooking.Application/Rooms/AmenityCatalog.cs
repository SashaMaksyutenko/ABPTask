using ConferenceBooking.Application.Rooms.Dtos;
using ConferenceBooking.Domain.Rooms;

namespace ConferenceBooking.Application.Rooms;

/// <summary>Перетворює послуги із запиту на позиції каталогу, створюючи відсутні.</summary>
public interface IAmenityCatalog
{
    /// <summary>
    /// Повертає пари «позиція каталогу — ціна для залу». Послуги, яких у каталозі ще немає,
    /// додаються автоматично: інакше адміністратор мусив би наповнювати довідник окремим
    /// запитом перед кожним створенням залу.
    /// </summary>
    Task<IReadOnlyList<(Amenity Amenity, decimal Price)>> ResolveAsync(
        IReadOnlyCollection<AmenityInput> requested,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IAmenityCatalog"/>
public sealed class AmenityCatalog : IAmenityCatalog
{
    private readonly IAmenityRepository _amenities;

    public AmenityCatalog(IAmenityRepository amenities) => _amenities = amenities;

    public async Task<IReadOnlyList<(Amenity Amenity, decimal Price)>> ResolveAsync(
        IReadOnlyCollection<AmenityInput> requested,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requested);

        // Кеш у межах одного запиту: щойно створені позиції ще не збережені в БД,
        // тому повторний пошук за назвою їх не знайшов би.
        var seen = new Dictionary<string, Amenity>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(Amenity, decimal)>(requested.Count);

        foreach (var input in requested)
        {
            var name = input.Name.Trim();

            if (!seen.TryGetValue(name, out var amenity))
            {
                amenity = await _amenities.FindByNameAsync(name, cancellationToken).ConfigureAwait(false);

                if (amenity is null)
                {
                    amenity = new Amenity(name, input.Price);
                    _amenities.Add(amenity);
                }

                seen[name] = amenity;
            }

            result.Add((amenity, input.Price));
        }

        return result;
    }
}
