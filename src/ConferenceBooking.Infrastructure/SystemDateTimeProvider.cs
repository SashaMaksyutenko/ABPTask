using ConferenceBooking.Application.Common;

namespace ConferenceBooking.Infrastructure;

/// <summary>
/// Системний годинник. Локальний час обчислюється у явно заданому часовому поясі закладу,
/// а не в поясі сервера: сервер може стояти в іншій країні, і тоді «вечірні години»
/// поїхали б відносно розкладу, який бачить адміністратор.
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    private readonly TimeZoneInfo _venueTimeZone;

    public SystemDateTimeProvider(TimeZoneInfo venueTimeZone) =>
        _venueTimeZone = venueTimeZone ?? throw new ArgumentNullException(nameof(venueTimeZone));

    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _venueTimeZone);
}
