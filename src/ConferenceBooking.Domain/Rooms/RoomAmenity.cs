using ConferenceBooking.Domain.Common;

namespace ConferenceBooking.Domain.Rooms;

/// <summary>
/// Послуга, доступна в конкретному залі, з ціною саме для цього залу.
/// Частина агрегату <see cref="ConferenceRoom"/> — створюється й змінюється лише через нього.
/// </summary>
public sealed class RoomAmenity : Entity
{
    /// <summary>Зал, до якого прив'язана послуга.</summary>
    public Guid RoomId { get; private set; }

    /// <summary>Позиція каталогу послуг.</summary>
    public Guid AmenityId { get; private set; }

    /// <summary>Навігація на каталог — потрібна, щоб віддавати назву послуги без зайвого запиту.</summary>
    public Amenity Amenity { get; private set; } = null!;

    /// <summary>Ціна послуги в цьому залі, грн.</summary>
    public decimal Price { get; private set; }

    private RoomAmenity()
    {
    }

    internal RoomAmenity(Guid roomId, Amenity amenity, decimal price)
    {
        ArgumentNullException.ThrowIfNull(amenity);
        Guard.AgainstNegativeMoney(price, nameof(price));

        RoomId = roomId;
        AmenityId = amenity.Id;
        Amenity = amenity;
        Price = price;
    }

    internal void ChangePrice(decimal price)
    {
        Guard.AgainstNegativeMoney(price, nameof(price));
        Price = price;
    }
}
