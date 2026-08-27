namespace ConferenceBooking.Application.Rooms.Dtos;

/// <summary>Послуга у складі запиту на створення/оновлення залу.</summary>
/// <param name="Name">Назва послуги, наприклад «Проєктор».</param>
/// <param name="Price">Ціна послуги в цьому залі, грн.</param>
public sealed record AmenityInput(string Name, decimal Price);

/// <summary>Запит на створення конференц-залу.</summary>
/// <param name="Name">Назва залу, наприклад «Зал А».</param>
/// <param name="Capacity">Місткість залу в особах.</param>
/// <param name="BasePricePerHour">Базова вартість оренди за годину, грн.</param>
/// <param name="Amenities">Перелік доступних послуг із цінами.</param>
public sealed record CreateRoomRequest(
    string Name,
    int Capacity,
    decimal BasePricePerHour,
    IReadOnlyList<AmenityInput>? Amenities);

/// <summary>
/// Запит на редагування залу. Усі поля необов'язкові: передаються лише ті, що змінюються.
/// Якщо передано <paramref name="Amenities"/>, перелік послуг залу замінюється повністю.
/// </summary>
public sealed record UpdateRoomRequest(
    string? Name,
    int? Capacity,
    decimal? BasePricePerHour,
    IReadOnlyList<AmenityInput>? Amenities);

/// <summary>Послуга залу у відповіді API.</summary>
public sealed record RoomAmenityResponse(Guid AmenityId, string Name, decimal Price);

/// <summary>Конференц-зал у відповіді API.</summary>
public sealed record RoomResponse(
    Guid Id,
    string Name,
    int Capacity,
    decimal BasePricePerHour,
    IReadOnlyList<RoomAmenityResponse> Amenities,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Підтвердження створення залу.</summary>
public sealed record RoomCreatedResponse(Guid Id, string Name, string Message);

/// <summary>Підтвердження виконання операції над залом.</summary>
public sealed record OperationResultResponse(Guid Id, string Message);
