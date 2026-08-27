namespace ConferenceBooking.Application.Common;

/// <summary>
/// Джерело поточного часу. Винесено за інтерфейс, щоб правила на кшталт
/// «не можна бронювати заднім числом» можна було перевірити тестами без залежності
/// від реального годинника машини.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>Поточний момент у локальному часі закладу.</summary>
    DateTime LocalNow { get; }

    /// <summary>Поточна дата в локальному часі закладу.</summary>
    DateOnly Today => DateOnly.FromDateTime(LocalNow);
}
