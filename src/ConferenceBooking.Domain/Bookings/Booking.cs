using ConferenceBooking.Domain.Common;
using ConferenceBooking.Domain.Pricing;

namespace ConferenceBooking.Domain.Bookings;

/// <summary>
/// Бронювання залу — корінь агрегату.
///
/// Вартість фіксується в момент створення бронювання і далі не перераховується:
/// зміна тарифів чи цін на послуги не повинна заднім числом змінювати суму,
/// яку клієнт уже погодив.
/// </summary>
public sealed class Booking : Entity
{
    /// <summary>Максимальна довжина імені замовника — узгоджена з обмеженням у схемі БД.</summary>
    public const int MaxCustomerNameLength = 200;

    /// <summary>Максимальна довжина email — узгоджена з обмеженням у схемі БД.</summary>
    public const int MaxCustomerEmailLength = 320;

    private readonly List<BookingAmenity> _amenities = [];

    /// <summary>Заброньований зал.</summary>
    public Guid RoomId { get; private set; }

    /// <summary>Назва залу знімком на момент бронювання — щоб звіти лишалися читабельними після перейменування.</summary>
    public string RoomName { get; private set; } = null!;

    /// <summary>Ім'я або назва замовника.</summary>
    public string CustomerName { get; private set; } = null!;

    /// <summary>Контактний email замовника.</summary>
    public string CustomerEmail { get; private set; } = null!;

    /// <summary>Кількість учасників.</summary>
    public int Attendees { get; private set; }

    /// <summary>Дата бронювання.</summary>
    public DateOnly Date { get; private set; }

    /// <summary>Початок бронювання (локальний час закладу). Денормалізовано для швидких запитів перетинів.</summary>
    public DateTime StartAt { get; private set; }

    /// <summary>Кінець бронювання (локальний час закладу).</summary>
    public DateTime EndAt { get; private set; }

    /// <summary>Статус бронювання.</summary>
    public BookingStatus Status { get; private set; } = BookingStatus.Confirmed;

    /// <summary>Вартість оренди залу без послуг, грн.</summary>
    public decimal RoomCost { get; private set; }

    /// <summary>Сумарна вартість замовлених послуг, грн.</summary>
    public decimal AmenitiesCost { get; private set; }

    /// <summary>Загальна вартість бронювання, грн.</summary>
    public decimal TotalCost { get; private set; }

    /// <summary>Момент створення бронювання (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    /// <summary>Момент скасування (UTC), якщо бронювання скасовано.</summary>
    public DateTime? CancelledAtUtc { get; private set; }

    /// <summary>Замовлені послуги знімком на момент бронювання.</summary>
    public IReadOnlyCollection<BookingAmenity> Amenities => _amenities.AsReadOnly();

    /// <summary>Період бронювання як об'єкт-значення.</summary>
    public BookingPeriod Period =>
        BookingPeriod.Create(Date, TimeOnly.FromDateTime(StartAt), TimeOnly.FromDateTime(EndAt));

    /// <summary>Тривалість бронювання в годинах.</summary>
    public decimal Hours => (decimal)(EndAt - StartAt).TotalHours;

    private Booking()
    {
    }

    public Booking(
        Guid roomId,
        string roomName,
        BookingPeriod period,
        string customerName,
        string customerEmail,
        int attendees,
        RentalCostBreakdown cost)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(cost);

        Id = Guid.NewGuid();

        if (attendees <= 0)
        {
            throw new DomainException("invalid_attendees", "Кількість учасників має бути більшою за нуль.");
        }

        RoomId = roomId;
        RoomName = Guard.AgainstNullOrWhiteSpace(roomName, "Назва залу", Rooms.ConferenceRoom.MaxNameLength);
        CustomerName = Guard.AgainstNullOrWhiteSpace(customerName, "Ім'я замовника", MaxCustomerNameLength);
        CustomerEmail = Guard.AgainstNullOrWhiteSpace(customerEmail, "Email замовника", MaxCustomerEmailLength);
        Attendees = attendees;

        Date = period.Date;
        StartAt = period.Start;
        EndAt = period.End;

        RoomCost = cost.RoomCost;
        AmenitiesCost = cost.AmenitiesCost;
        TotalCost = cost.Total;

        foreach (var amenity in cost.Amenities)
        {
            _amenities.Add(new BookingAmenity(Id, amenity.AmenityId, amenity.Name, amenity.Price));
        }
    }

    /// <summary>
    /// Скасовує бронювання. Повторне скасування — помилка бізнес-правила, а не мовчазний no-op:
    /// клієнт має розуміти, що його друга спроба нічого не змінила.
    /// </summary>
    public void Cancel()
    {
        if (Status == BookingStatus.Cancelled)
        {
            throw new ConflictException("booking_already_cancelled", "Бронювання вже скасовано.");
        }

        Status = BookingStatus.Cancelled;
        CancelledAtUtc = DateTime.UtcNow;
    }

    /// <summary>Чи блокує це бронювання зал (скасовані бронювання зал не займають).</summary>
    public bool IsBlocking => Status == BookingStatus.Confirmed;
}
