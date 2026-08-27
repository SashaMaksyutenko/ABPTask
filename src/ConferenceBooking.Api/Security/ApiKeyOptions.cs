namespace ConferenceBooking.Api.Security;

/// <summary>Один виданий ключ доступу.</summary>
public sealed class ApiKeyEntry
{
    /// <summary>Сам ключ. У продакшені береться зі сховища секретів, а не з appsettings.json.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Кому видано ключ — потрапляє в логи, щоб дії можна було відстежити.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>Роль власника ключа: <see cref="ApiRoles"/>.</summary>
    public string Role { get; set; } = ApiRoles.Client;
}

/// <summary>Налаштування автентифікації за API-ключем.</summary>
public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKeys";

    /// <summary>Заголовок, у якому очікується ключ.</summary>
    public string HeaderName { get; set; } = "X-Api-Key";

    /// <summary>Перелік виданих ключів.</summary>
    public List<ApiKeyEntry> Keys { get; set; } = [];
}

/// <summary>Ролі, доступні в API.</summary>
public static class ApiRoles
{
    /// <summary>Адміністратор закладу: керує залами та бачить аналітику.</summary>
    public const string Administrator = "Administrator";

    /// <summary>Клієнт: шукає зали, бронює й керує власними бронюваннями.</summary>
    public const string Client = "Client";
}

/// <summary>Політики авторизації.</summary>
public static class ApiPolicies
{
    /// <summary>Створення, редагування та видалення залів — лише адміністратор.</summary>
    public const string ManageRooms = "ManageRooms";

    /// <summary>Перегляд бізнес-звітів — лише адміністратор.</summary>
    public const string ViewReports = "ViewReports";

    /// <summary>Пошук і бронювання — будь-який автентифікований клієнт.</summary>
    public const string BookRooms = "BookRooms";
}
