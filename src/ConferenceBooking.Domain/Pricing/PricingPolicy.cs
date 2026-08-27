using ConferenceBooking.Domain.Common;

namespace ConferenceBooking.Domain.Pricing;

/// <summary>
/// Тарифна політика: набір тарифних смуг доби та робочі години закладу.
/// Політика приходить з конфігурації (appsettings), тому змінити знижку чи додати нову смугу
/// можна без правок коду — це вимога масштабованості з ТЗ.
/// </summary>
public sealed class PricingPolicy
{
    private readonly IReadOnlyList<PricingBand> _bandsByPriority;

    /// <summary>Усі тарифні смуги політики.</summary>
    public IReadOnlyList<PricingBand> Bands { get; }

    /// <summary>Найраніший час, з якого можна починати бронювання.</summary>
    public TimeOnly OpeningTime { get; }

    /// <summary>Найпізніший час, яким може завершуватися бронювання.</summary>
    public TimeOnly ClosingTime { get; }

    public PricingPolicy(IEnumerable<PricingBand> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);

        Bands = bands.OrderBy(b => b.Start).ToArray();
        if (Bands.Count == 0)
        {
            throw new ArgumentException("Тарифна політика має містити хоча б одну тарифну смугу.", nameof(bands));
        }

        // Пошук смуги для конкретної миті йде від найвищого пріоритету до найнижчого,
        // тож упорядковуємо один раз у конструкторі, а не на кожному зверненні.
        _bandsByPriority = Bands.OrderByDescending(b => b.Priority).ToArray();

        OpeningTime = Bands.Min(b => b.Start);
        ClosingTime = Bands.Max(b => b.End);

        EnsureNoGapsInsideWorkingHours();
    }

    /// <summary>
    /// Повертає тарифну смугу, що діє у вказану мить, або <c>null</c>, якщо заклад у цей час зачинено.
    /// </summary>
    public PricingBand? FindBand(TimeOnly moment) =>
        _bandsByPriority.FirstOrDefault(band => band.Contains(moment));

    /// <summary>
    /// Межі тарифних смуг у межах робочого дня, відсортовані за зростанням.
    /// Використовуються, щоб розрізати бронювання на ділянки з однаковою ставкою.
    /// </summary>
    public IReadOnlyList<TimeOnly> BandBoundaries() =>
        Bands.SelectMany(b => new[] { b.Start, b.End })
             .Distinct()
             .OrderBy(t => t)
             .ToArray();

    /// <summary>
    /// Перевіряє, що всередині робочих годин немає «дірок»: інакше бронювання могло б
    /// потрапити на проміжок без ціни й тихо порахуватися як безкоштовне.
    /// </summary>
    private void EnsureNoGapsInsideWorkingHours()
    {
        foreach (var boundary in BandBoundaries().Where(t => t >= OpeningTime && t < ClosingTime))
        {
            if (FindBand(boundary) is null)
            {
                throw new DomainException(
                    "pricing_policy_gap",
                    $"У тарифній політиці немає ставки для часу {boundary:HH\\:mm}.");
            }
        }
    }
}
