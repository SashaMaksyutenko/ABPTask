using ConferenceBooking.Domain.Bookings;

namespace ConferenceBooking.Domain.Rooms;

/// <summary>
/// Доступ до конференц-залів. Інтерфейс живе в домені, реалізація — в інфраструктурі,
/// щоб доменні та прикладні правила не залежали від EF Core і конкретної СУБД.
/// </summary>
public interface IConferenceRoomRepository
{
    /// <summary>Повертає зал разом із його послугами або <c>null</c>, якщо залу немає чи його видалено.</summary>
    Task<ConferenceRoom?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Усі активні зали.</summary>
    Task<IReadOnlyList<ConferenceRoom>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Чи існує активний зал із такою назвою. <paramref name="excludeRoomId"/> дозволяє
    /// не рахувати сам себе під час редагування.
    /// </summary>
    Task<bool> NameExistsAsync(
        string name,
        Guid? excludeRoomId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Зали, що вільні у вказаний період і вміщують потрібну кількість осіб.
    /// </summary>
    Task<IReadOnlyList<ConferenceRoom>> FindAvailableAsync(
        BookingPeriod period,
        int minimumCapacity,
        CancellationToken cancellationToken = default);

    void Add(ConferenceRoom room);
}
