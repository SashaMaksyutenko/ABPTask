namespace ConferenceBooking.Domain.Bookings;

/// <summary>Статус бронювання.</summary>
public enum BookingStatus
{
    /// <summary>Підтверджене бронювання: зал зайнято, вартість зафіксовано.</summary>
    Confirmed = 1,

    /// <summary>Скасоване бронювання: зал вільний, запис лишається для звітності.</summary>
    Cancelled = 2
}
