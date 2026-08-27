using ConferenceBooking.Domain.Common;

namespace ConferenceBooking.Domain.Rooms;

/// <summary>
/// Конференц-зал — корінь агрегату. Володіє власним переліком доступних послуг
/// і єдиний відповідає за їхню консистентність.
/// </summary>
public sealed class ConferenceRoom : Entity
{
    /// <summary>Максимальна довжина назви залу — узгоджена з обмеженням у схемі БД.</summary>
    public const int MaxNameLength = 200;

    /// <summary>Верхня межа місткості — санітарна перевірка вхідних даних.</summary>
    public const int MaxCapacity = 10_000;

    private readonly List<RoomAmenity> _amenities = [];

    /// <summary>Назва залу, наприклад «Зал А».</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Нормалізована назва — за нею працюють унікальний індекс і перевірка на дублікати.</summary>
    public string NormalizedName { get; private set; } = null!;

    /// <summary>Місткість залу в особах.</summary>
    public int Capacity { get; private set; }

    /// <summary>Базова вартість оренди за годину, грн (до застосування тарифних смуг).</summary>
    public decimal BasePricePerHour { get; private set; }

    /// <summary>
    /// Ознака «видалено». Використовується м'яке видалення: історія бронювань і фінансова
    /// звітність за минулі періоди мають лишатися коректними після видалення залу.
    /// </summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Момент створення запису (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    /// <summary>Момент останньої зміни (UTC).</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Послуги, доступні в цьому залі.</summary>
    public IReadOnlyCollection<RoomAmenity> Amenities => _amenities.AsReadOnly();

    private ConferenceRoom()
    {
    }

    public ConferenceRoom(string name, int capacity, decimal basePricePerHour)
    {
        Id = Guid.NewGuid();

        Rename(name);
        ChangeCapacity(capacity);
        ChangeBasePrice(basePricePerHour);

        // Сеттери позначають запис як змінений; для щойно створеного залу змін ще не було.
        UpdatedAtUtc = null;
    }

    public void Rename(string name)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, "Назва залу", MaxNameLength);
        NormalizedName = NameNormalizer.Normalize(Name);
        Touch();
    }

    public void ChangeCapacity(int capacity)
    {
        Guard.AgainstInvalidCapacity(capacity, MaxCapacity);
        Capacity = capacity;
        Touch();
    }

    public void ChangeBasePrice(decimal basePricePerHour)
    {
        Guard.AgainstNegativeMoney(basePricePerHour, "Базова вартість за годину");
        BasePricePerHour = basePricePerHour;
        Touch();
    }

    /// <summary>
    /// Додає послугу до залу або оновлює її ціну, якщо послуга вже доступна.
    /// Ідемпотентність тут навмисна: повторний виклик із тією ж послугою не має падати помилкою.
    /// </summary>
    public RoomAmenity AddOrUpdateAmenity(Amenity amenity, decimal price)
    {
        ArgumentNullException.ThrowIfNull(amenity);

        var existing = _amenities.SingleOrDefault(a => a.AmenityId == amenity.Id);
        if (existing is not null)
        {
            existing.ChangePrice(price);
            Touch();
            return existing;
        }

        var added = new RoomAmenity(Id, amenity, price);
        _amenities.Add(added);
        Touch();
        return added;
    }

    /// <summary>Прибирає послугу із залу. Повертає <c>false</c>, якщо такої послуги там не було.</summary>
    public bool RemoveAmenity(Guid amenityId)
    {
        var removed = _amenities.RemoveAll(a => a.AmenityId == amenityId) > 0;
        if (removed)
        {
            Touch();
        }

        return removed;
    }

    /// <summary>Замінює весь перелік послуг залу на переданий.</summary>
    public void ReplaceAmenities(IEnumerable<(Amenity Amenity, decimal Price)> amenities)
    {
        ArgumentNullException.ThrowIfNull(amenities);

        var replacement = amenities.ToArray();
        var duplicate = replacement.GroupBy(a => a.Amenity.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new DomainException(
                "duplicate_amenity",
                $"Послугу «{duplicate.First().Amenity.Name}» вказано у списку більше одного разу.");
        }

        _amenities.Clear();
        foreach (var (amenity, price) in replacement)
        {
            _amenities.Add(new RoomAmenity(Id, amenity, price));
        }

        Touch();
    }

    /// <summary>
    /// Знаходить послуги залу за їхніми ідентифікаторами.
    /// Кидає помилку, якщо клієнт замовив послугу, якої в цьому залі немає — так замовник
    /// одразу бачить причину, замість тихо недоплатити за неіснуючу послугу.
    /// </summary>
    public IReadOnlyList<RoomAmenity> ResolveAmenities(IEnumerable<Guid> amenityIds)
    {
        ArgumentNullException.ThrowIfNull(amenityIds);

        var requested = amenityIds.Distinct().ToArray();
        var resolved = new List<RoomAmenity>(requested.Length);

        foreach (var amenityId in requested)
        {
            var amenity = _amenities.SingleOrDefault(a => a.AmenityId == amenityId)
                ?? throw new DomainException(
                    "amenity_not_available",
                    $"Послуга {amenityId} недоступна в залі «{Name}».");

            resolved.Add(amenity);
        }

        return resolved;
    }

    /// <summary>Чи вміщує зал вказану кількість учасників.</summary>
    public bool CanAccommodate(int attendees) => attendees > 0 && attendees <= Capacity;

    /// <summary>М'яке видалення залу.</summary>
    public void MarkDeleted()
    {
        IsDeleted = true;
        Touch();
    }

    private void Touch() => UpdatedAtUtc = DateTime.UtcNow;
}
