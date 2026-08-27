namespace ConferenceBooking.Domain.Common;

/// <summary>
/// Порушення бізнес-правила домену (наприклад, спроба забронювати зал на зайнятий час).
/// Такі помилки очікувані, тому транслюються клієнту як HTTP 409/422, а не як 500.
/// </summary>
public class DomainException : Exception
{
    /// <summary>Машиночитний код помилки — щоб клієнт міг реагувати програмно, не парсячи текст.</summary>
    public string Code { get; }

    public DomainException(string code, string message) : base(message) => Code = code;
}

/// <summary>Запитаний ресурс не знайдено. Транслюється у HTTP 404.</summary>
public sealed class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName, Guid id)
        : base("entity_not_found", $"{entityName} з ідентифікатором {id} не знайдено.")
    {
    }
}

/// <summary>Конфлікт стану — час зайнято, назва вже використана тощо. Транслюється у HTTP 409.</summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string code, string message) : base(code, message)
    {
    }
}
