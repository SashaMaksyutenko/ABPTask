using ConferenceBooking.Application.Rooms.Dtos;
using ConferenceBooking.Domain.Rooms;

namespace ConferenceBooking.Application.Rooms;

/// <summary>
/// Перетворення доменних сутностей на контракти API.
/// Мапінг написаний вручну, без AutoMapper: контрактів небагато, а явний код
/// дешевше читати й неможливо зламати непомітно під час рефакторингу.
/// </summary>
public static class RoomMapper
{
    public static RoomResponse ToResponse(this ConferenceRoom room) =>
        new(
            room.Id,
            room.Name,
            room.Capacity,
            room.BasePricePerHour,
            room.Amenities.Select(ToResponse).OrderBy(a => a.Name).ToArray(),
            room.CreatedAtUtc,
            room.UpdatedAtUtc);

    public static RoomAmenityResponse ToResponse(this RoomAmenity amenity) =>
        new(amenity.AmenityId, amenity.Amenity.Name, amenity.Price);
}
