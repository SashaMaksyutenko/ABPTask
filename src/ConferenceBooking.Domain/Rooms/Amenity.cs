using ConferenceBooking.Domain.Common;

namespace ConferenceBooking.Domain.Rooms;

/// <summary>
/// Послуга з каталогу закладу (проєктор, Wi-Fi, звук).
///
/// Каталог зберігає назву та типову ціну. Фактична ціна для конкретного залу живе
/// в <see cref="RoomAmenity"/>: та сама послуга може коштувати по-різному у різних залах.
/// </summary>
public sealed class Amenity : Entity
{
    /// <summary>Максимальна довжина назви — узгоджена з обмеженням у схемі БД.</summary>
    public const int MaxNameLength = 100;

    /// <summary>Назва послуги. Унікальна в межах каталогу (без урахування регістру).</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Нормалізована назва — за нею працюють унікальний індекс і пошук.</summary>
    public string NormalizedName { get; private set; } = null!;

    /// <summary>Типова ціна послуги, грн. Використовується, якщо для залу ціну не вказано явно.</summary>
    public decimal DefaultPrice { get; private set; }

    // Потрібен EF Core для матеріалізації.
    private Amenity()
    {
    }

    public Amenity(string name, decimal defaultPrice)
    {
        Id = Guid.NewGuid();

        Rename(name);
        ChangeDefaultPrice(defaultPrice);
    }

    public void Rename(string name)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, "Назва послуги", MaxNameLength);
        NormalizedName = NameNormalizer.Normalize(Name);
    }

    public void ChangeDefaultPrice(decimal price)
    {
        Guard.AgainstNegativeMoney(price, nameof(price));
        DefaultPrice = price;
    }
}
