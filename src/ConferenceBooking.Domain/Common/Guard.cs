namespace ConferenceBooking.Domain.Common;

/// <summary>
/// Перевірки інваріантів домену. Винесені в одне місце, щоб правила не розповзалися
/// копіпастом по сутностях і щоб повідомлення про помилки були однаковими всюди.
/// </summary>
public static class Guard
{
    /// <summary>Максимальна допустима сума в гривнях — захист від переповнення та явних помилок вводу.</summary>
    public const decimal MaxMoney = 10_000_000m;

    /// <summary>
    /// Перевіряє, що рядок не порожній і не довший за ліміт. Повертає значення без крайніх пробілів.
    /// </summary>
    public static string AgainstNullOrWhiteSpace(string? value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("value_required", $"Поле «{parameterName}» є обов'язковим.");
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new DomainException(
                "value_too_long",
                $"Поле «{parameterName}» не може бути довшим за {maxLength} символів.");
        }

        return trimmed;
    }

    /// <summary>Перевіряє, що грошова сума невід'ємна і в межах розумного діапазону.</summary>
    public static void AgainstNegativeMoney(decimal value, string parameterName)
    {
        if (value < 0)
        {
            throw new DomainException("negative_amount", $"Поле «{parameterName}» не може бути від'ємним.");
        }

        if (value > MaxMoney)
        {
            throw new DomainException(
                "amount_too_large",
                $"Поле «{parameterName}» не може перевищувати {MaxMoney:N0} грн.");
        }
    }

    /// <summary>Перевіряє, що місткість — додатне і правдоподібне число.</summary>
    public static void AgainstInvalidCapacity(int capacity, int maxCapacity)
    {
        if (capacity <= 0)
        {
            throw new DomainException("invalid_capacity", "Місткість залу має бути більшою за нуль.");
        }

        if (capacity > maxCapacity)
        {
            throw new DomainException(
                "invalid_capacity",
                $"Місткість залу не може перевищувати {maxCapacity} осіб.");
        }
    }
}
