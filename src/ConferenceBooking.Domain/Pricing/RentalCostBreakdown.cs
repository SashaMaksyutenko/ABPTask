namespace ConferenceBooking.Domain.Pricing;

/// <summary>
/// Ділянка бронювання з єдиною тарифною ставкою.
/// Бронювання 10:00–14:00 розпадається на дві ділянки: 10:00–12:00 (стандарт) і 12:00–14:00 (пік).
/// </summary>
/// <param name="BandName">Назва тарифної смуги.</param>
/// <param name="From">Початок ділянки.</param>
/// <param name="To">Кінець ділянки.</param>
/// <param name="Hours">Тривалість ділянки в годинах.</param>
/// <param name="Multiplier">Коефіцієнт до базової ставки.</param>
/// <param name="Amount">Вартість ділянки, грн.</param>
public sealed record RentalCostSegment(
    string BandName,
    TimeOnly From,
    TimeOnly To,
    decimal Hours,
    decimal Multiplier,
    decimal Amount);

/// <summary>Позиція «послуга» у рахунку.</summary>
/// <param name="AmenityId">Ідентифікатор послуги в каталозі.</param>
/// <param name="Name">Назва послуги на момент бронювання.</param>
/// <param name="Price">Ціна послуги на момент бронювання, грн.</param>
public sealed record AmenityCharge(Guid AmenityId, string Name, decimal Price);

/// <summary>
/// Повна деталізація вартості оренди. Повертається клієнту разом із підтвердженням бронювання,
/// щоб вартість була прозорою і не викликала суперечок.
/// </summary>
public sealed record RentalCostBreakdown(
    IReadOnlyList<RentalCostSegment> Segments,
    decimal RoomCost,
    IReadOnlyList<AmenityCharge> Amenities,
    decimal AmenitiesCost,
    decimal Total);
