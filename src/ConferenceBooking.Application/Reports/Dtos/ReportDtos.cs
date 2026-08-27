namespace ConferenceBooking.Application.Reports.Dtos;

/// <summary>Проміжок дат, за який побудовано звіт (включно з обома межами).</summary>
public sealed record ReportPeriod(DateOnly From, DateOnly To);

/// <summary>Крок групування у звіті про виторг.</summary>
public enum RevenueGranularity
{
    Day = 1,
    Week = 2,
    Month = 3
}

/// <summary>
/// Зведені показники за період — верхньорівнева картина для керівництва:
/// скільки заробили, наскільки завантажені, скільки бронювань зривається.
/// </summary>
public sealed record SummaryReport(
    ReportPeriod Period,
    int TotalBookings,
    int ConfirmedBookings,
    int CancelledBookings,
    decimal CancellationRatePercent,
    decimal TotalRevenue,
    decimal RoomRevenue,
    decimal AmenitiesRevenue,
    decimal AverageBookingValue,
    decimal AverageDurationHours,
    decimal TotalBookedHours,
    int UniqueCustomers,
    string? TopRoomName,
    string? TopAmenityName);

/// <summary>Завантаженість одного залу.</summary>
public sealed record RoomUtilizationRow(
    Guid RoomId,
    string RoomName,
    int Capacity,
    int Bookings,
    decimal BookedHours,
    decimal AvailableHours,
    decimal UtilizationPercent,
    decimal Revenue,
    decimal RevenuePerHour,
    decimal AverageAttendees,
    decimal AverageFillPercent);

/// <summary>
/// Завантаженість залів. Показує, який зал недозавантажений (кандидат на знижку чи
/// перепрофілювання), а який стабільно заповнений (кандидат на підвищення ціни).
/// </summary>
public sealed record RoomUtilizationReport(
    ReportPeriod Period,
    decimal WorkingHoursPerDay,
    IReadOnlyList<RoomUtilizationRow> Rooms);

/// <summary>Виторг за один інтервал групування.</summary>
public sealed record RevenueBucket(
    string Label,
    DateOnly From,
    DateOnly To,
    int Bookings,
    decimal RoomRevenue,
    decimal AmenitiesRevenue,
    decimal TotalRevenue);

/// <summary>Динаміка виторгу в часі — для планування та виявлення сезонності.</summary>
public sealed record RevenueReport(
    ReportPeriod Period,
    RevenueGranularity Granularity,
    decimal TotalRevenue,
    IReadOnlyList<RevenueBucket> Buckets);

/// <summary>Попит на одну послугу.</summary>
public sealed record AmenityDemandRow(
    Guid AmenityId,
    string Name,
    int TimesOrdered,
    decimal AttachRatePercent,
    decimal Revenue,
    decimal AveragePrice);

/// <summary>
/// Попит на послуги. Відповідає на питання «що реально купують»: які послуги варто
/// докупити в інші зали, а які не окуповують обслуговування.
/// </summary>
public sealed record AmenityDemandReport(
    ReportPeriod Period,
    int ConfirmedBookings,
    IReadOnlyList<AmenityDemandRow> Amenities);

/// <summary>Завантаженість однієї години доби.</summary>
public sealed record HourlyLoadRow(
    TimeOnly Hour,
    int Bookings,
    decimal BookedHours,
    decimal Revenue);

/// <summary>
/// Розподіл попиту за годинами доби. Дає фактичну відповідь на питання, чи збігаються
/// «пікові години» з тарифів із реальним піком попиту.
/// </summary>
public sealed record HourlyLoadReport(
    ReportPeriod Period,
    IReadOnlyList<HourlyLoadRow> Hours);

/// <summary>Результат по одній тарифній смузі.</summary>
public sealed record PricingBandRow(
    string Band,
    decimal Multiplier,
    decimal BookedHours,
    decimal SharePercent,
    decimal Revenue,
    decimal RevenueAtBaseRate,
    decimal DiscountOrSurcharge);

/// <summary>
/// Ефективність тарифної сітки: скільки годин продано в кожній смузі та у скільки
/// бізнесу обійшлися знижки (чи скільки принесли націнки) порівняно з базовим тарифом.
/// </summary>
public sealed record PricingBandReport(
    ReportPeriod Period,
    decimal TotalRoomRevenue,
    decimal TotalDiscountOrSurcharge,
    IReadOnlyList<PricingBandRow> Bands);
