using ConferenceBooking.Application.Rooms.Dtos;

namespace ConferenceBooking.Application.Bookings.Dtos;

/// <summary>Запит на пошук вільних залів.</summary>
/// <param name="Date">Дата, на яку шукаємо зал.</param>
/// <param name="StartTime">Початок потрібного проміжку.</param>
/// <param name="EndTime">Кінець потрібного проміжку.</param>
/// <param name="Capacity">Мінімальна потрібна місткість, осіб.</param>
public sealed record AvailabilitySearchRequest(
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int Capacity);

/// <summary>
/// Вільний зал у результатах пошуку. Разом із залом одразу віддається орієнтовна вартість
/// саме на цей проміжок: інакше клієнтові довелося б робити ще один запит на кожен зал,
/// щоб просто порівняти ціни.
/// </summary>
public sealed record AvailableRoomResponse(
    Guid Id,
    string Name,
    int Capacity,
    decimal BasePricePerHour,
    IReadOnlyList<RoomAmenityResponse> Amenities,
    decimal EstimatedRoomCost);

/// <summary>Запит на бронювання залу.</summary>
/// <param name="RoomId">Ідентифікатор залу.</param>
/// <param name="Date">Дата бронювання.</param>
/// <param name="StartTime">Час початку.</param>
/// <param name="DurationMinutes">Тривалість у хвилинах.</param>
/// <param name="Attendees">Кількість учасників.</param>
/// <param name="CustomerName">Ім'я або назва замовника.</param>
/// <param name="CustomerEmail">Контактний email замовника.</param>
/// <param name="AmenityIds">Обрані послуги залу.</param>
public sealed record CreateBookingRequest(
    Guid RoomId,
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMinutes,
    int Attendees,
    string CustomerName,
    string CustomerEmail,
    IReadOnlyList<Guid>? AmenityIds);

/// <summary>Запит на попередній розрахунок вартості без створення бронювання.</summary>
public sealed record QuoteRequest(
    Guid RoomId,
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMinutes,
    IReadOnlyList<Guid>? AmenityIds);

/// <summary>Ділянка бронювання з окремою тарифною ставкою.</summary>
public sealed record CostSegmentResponse(
    string Band,
    TimeOnly From,
    TimeOnly To,
    decimal Hours,
    decimal Multiplier,
    decimal Amount);

/// <summary>Позиція «послуга» в рахунку.</summary>
public sealed record ChargedAmenityResponse(Guid AmenityId, string Name, decimal Price);

/// <summary>Деталізація вартості оренди.</summary>
public sealed record CostBreakdownResponse(
    decimal RoomCost,
    decimal AmenitiesCost,
    decimal Total,
    IReadOnlyList<CostSegmentResponse> Segments,
    IReadOnlyList<ChargedAmenityResponse> Amenities);

/// <summary>Бронювання у відповіді API.</summary>
public sealed record BookingResponse(
    Guid Id,
    Guid RoomId,
    string RoomName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal DurationHours,
    int Attendees,
    string CustomerName,
    string CustomerEmail,
    string Status,
    CostBreakdownResponse Cost,
    DateTime CreatedAtUtc);
