namespace ConferenceBooking.Domain.Pricing;

/// <summary>
/// Тарифна смуга — проміжок доби, у якому діє власний коефіцієнт до базової вартості залу.
/// Наприклад: «Вечірні години 18:00–23:00, знижка 20%» → <see cref="Multiplier"/> = 0.80.
/// </summary>
/// <param name="Name">Людиночитна назва смуги (потрапляє у деталізацію рахунку).</param>
/// <param name="Start">Початок смуги (включно).</param>
/// <param name="End">Кінець смуги (не включно).</param>
/// <param name="Multiplier">Коефіцієнт до базової погодинної ставки: 0.80 = −20%, 1.15 = +15%.</param>
/// <param name="Priority">
/// Пріоритет при накладанні смуг. Пікові години (12:00–14:00) лежать усередині стандартних
/// (09:00–18:00), тому смуга з вищим пріоритетом перекриває смугу з нижчим.
/// </param>
public sealed record PricingBand(
    string Name,
    TimeOnly Start,
    TimeOnly End,
    decimal Multiplier,
    int Priority)
{
    /// <summary>Чи належить мить <paramref name="moment"/> цій смузі. Межа кінця не включається.</summary>
    public bool Contains(TimeOnly moment) => moment >= Start && moment < End;
}
