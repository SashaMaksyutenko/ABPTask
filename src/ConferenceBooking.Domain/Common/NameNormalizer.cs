namespace ConferenceBooking.Domain.Common;

/// <summary>
/// Приводить назви до канонічного вигляду для порівнянь і унікальних індексів.
///
/// Покладатися на колацію БД тут не можна: SQLite-колація NOCASE згортає регістр
/// лише для латиниці ASCII, тож «Зал А» і «зал а» вона вважала б різними назвами.
/// Окрема нормалізована колонка робить поведінку однаковою для будь-якої мови
/// і не залежить від провайдера БД.
/// </summary>
public static class NameNormalizer
{
    /// <summary>Нормалізована форма назви: без крайніх пробілів, у верхньому регістрі.</summary>
    public static string Normalize(string value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();
}
