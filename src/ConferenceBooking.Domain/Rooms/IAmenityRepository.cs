namespace ConferenceBooking.Domain.Rooms;

/// <summary>Доступ до каталогу послуг.</summary>
public interface IAmenityRepository
{
    /// <summary>Пошук послуги за назвою без урахування регістру.</summary>
    Task<Amenity?> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    void Add(Amenity amenity);
}
