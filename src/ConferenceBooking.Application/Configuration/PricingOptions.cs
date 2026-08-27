using ConferenceBooking.Domain.Common;
using ConferenceBooking.Domain.Pricing;

namespace ConferenceBooking.Application.Configuration;

/// <summary>Опис однієї тарифної смуги в конфігурації.</summary>
public sealed class PricingBandOptions
{
    /// <summary>Назва смуги, наприклад «Пікові години».</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Початок смуги у форматі HH:mm.</summary>
    public string Start { get; set; } = string.Empty;

    /// <summary>Кінець смуги у форматі HH:mm.</summary>
    public string End { get; set; } = string.Empty;

    /// <summary>Коефіцієнт до базової ставки: 0.8 = знижка 20%, 1.15 = націнка 15%.</summary>
    public decimal Multiplier { get; set; } = 1m;

    /// <summary>Пріоритет при перекритті смуг: більше значення перемагає.</summary>
    public int Priority { get; set; }
}

/// <summary>
/// Тарифна сітка з конфігурації. Виділена в налаштування свідомо: бізнес змінює знижки
/// й години частіше, ніж виходять релізи, — і для цього не має знадобитися програміст.
/// </summary>
public sealed class PricingOptions
{
    public const string SectionName = "Pricing";

    public List<PricingBandOptions> Bands { get; set; } = [];

    /// <summary>Збирає доменну тарифну політику з конфігурації, перевіряючи коректність значень.</summary>
    public PricingPolicy ToPolicy()
    {
        if (Bands.Count == 0)
        {
            throw new DomainException(
                "pricing_not_configured",
                $"Секція конфігурації «{SectionName}:Bands» порожня — тарифну сітку не задано.");
        }

        return new PricingPolicy(Bands.Select(ToBand));
    }

    private static PricingBand ToBand(PricingBandOptions options)
    {
        var name = Guard.AgainstNullOrWhiteSpace(options.Name, "Pricing:Bands:Name", 100);

        var start = ParseTime(options.Start, $"Pricing:Bands:{name}:Start");
        var end = ParseTime(options.End, $"Pricing:Bands:{name}:End");

        if (end <= start)
        {
            throw new DomainException(
                "invalid_pricing_band",
                $"Тарифна смуга «{name}»: кінець має бути пізнішим за початок.");
        }

        if (options.Multiplier <= 0)
        {
            throw new DomainException(
                "invalid_pricing_band",
                $"Тарифна смуга «{name}»: коефіцієнт має бути додатним.");
        }

        return new PricingBand(name, start, end, options.Multiplier, options.Priority);
    }

    private static TimeOnly ParseTime(string value, string path) =>
        TimeOnly.TryParseExact(value, "HH\\:mm", out var parsed)
            ? parsed
            : throw new DomainException(
                "invalid_pricing_band",
                $"Значення «{path}» має бути часом у форматі HH:mm, отримано «{value}».");
}
