using ConferenceBooking.Domain.Common;

namespace ConferenceBooking.Domain.Bookings;

/// <summary>
/// Послуга, замовлена в межах бронювання.
///
/// Назва й ціна зберігаються знімком на момент бронювання: якщо завтра проєктор подорожчає
/// або послугу приберуть із залу, вже виставлений рахунок не має «поїхати».
/// </summary>
public sealed class BookingAmenity : Entity
{
    /// <summary>Бронювання, до якого належить послуга.</summary>
    public Guid BookingId { get; private set; }

    /// <summary>Позиція каталогу послуг — для аналітики за послугами.</summary>
    public Guid AmenityId { get; private set; }

    /// <summary>Назва послуги на момент бронювання.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Ціна послуги на момент бронювання, грн.</summary>
    public decimal Price { get; private set; }

    private BookingAmenity()
    {
    }

    internal BookingAmenity(Guid bookingId, Guid amenityId, string name, decimal price)
    {
        Guard.AgainstNegativeMoney(price, nameof(price));

        BookingId = bookingId;
        AmenityId = amenityId;
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name), Rooms.Amenity.MaxNameLength);
        Price = price;
    }
}
