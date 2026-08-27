namespace ConferenceBooking.Application.Configuration;

/// <summary>Правила прийому бронювань, які бізнес може змінювати без релізу.</summary>
public sealed class BookingPolicyOptions
{
    public const string SectionName = "BookingPolicy";

    /// <summary>Наскільки далеко наперед приймаються бронювання, днів.</summary>
    public int MaxAdvanceDays { get; set; } = 365;

    /// <summary>
    /// Крок сітки бронювання у хвилинах. 30 означає, що час має бути кратним півгодині —
    /// це прибирає «рвані» проміжки на кшталт 10:07, які неможливо продати.
    /// </summary>
    public int SlotSizeMinutes { get; set; } = 30;

    /// <summary>Максимальна кількість учасників, яку приймає система.</summary>
    public int MaxAttendees { get; set; } = 10_000;
}
