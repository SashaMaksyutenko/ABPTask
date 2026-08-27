# Conference Booking API

API для управління конференц-залами, бронюваннями та розрахунку вартості оренди.

Реалізація тестового завдання [BackendTZ.pdf](BackendTZ.pdf): облік залів і послуг,
пошук вільних приміщень, бронювання з розрахунком вартості за тарифними смугами доби,
а також набір бізнес-звітів.

---

## Зміст

- [Швидкий старт](#швидкий-старт)
- [Автентифікація](#автентифікація)
- [Методи API](#методи-api)
- [Розрахунок вартості оренди](#розрахунок-вартості-оренди)
- [Звіти](#звіти)
- [Архітектура](#архітектура)
- [Безпека](#безпека)
- [Конфігурація](#конфігурація)
- [Тести](#тести)
- [Початкові дані](#початкові-дані)

---

## Швидкий старт

**Потрібно:** .NET SDK 9.0 або новіший. Зовнішня СУБД не потрібна — використовується SQLite.

```bash
git clone <repository-url>
cd ABPTask

dotnet restore
dotnet run --project src/ConferenceBooking.Api
```

Під час першого запуску застосунок сам застосує міграції та створить початкові дані з ТЗ.

| Ресурс | Адреса |
|---|---|
| Swagger UI | <http://localhost:5199/swagger> |
| OpenAPI-специфікація | <http://localhost:5199/swagger/v1/swagger.json> |
| Health check | <http://localhost:5199/health> |

> Порт задається в `src/ConferenceBooking.Api/Properties/launchSettings.json`
> або змінною середовища `ASPNETCORE_URLS`.

---

## Автентифікація

Усі методи, крім `/health`, вимагають API-ключ у заголовку `X-Api-Key`.

Демонстраційні ключі (`appsettings.json`):

| Ключ | Роль | Права |
|---|---|---|
| `admin-demo-key-3f9a2c` | `Administrator` | Усе: керування залами, бронювання, звіти |
| `client-demo-key-8b1e57` | `Client` | Перегляд залів, пошук, бронювання |

```bash
curl -H "X-Api-Key: client-demo-key-8b1e57" http://localhost:5199/api/rooms
```

У Swagger UI натисніть **Authorize** і вставте ключ.

> ⚠️ Ключі в `appsettings.json` — демонстраційні. У продакшені вони мають надходити
> зі сховища секретів (Azure Key Vault, AWS Secrets Manager, змінні середовища),
> а не з файлу в репозиторії.

---

## Методи API

### Зали

| Метод | Маршрут | Роль | Опис |
|---|---|---|---|
| `POST` | `/api/rooms` | Administrator | **Додавання конференц-залу** |
| `PATCH` | `/api/rooms/{id}` | Administrator | **Редагування інформації про зал** |
| `DELETE` | `/api/rooms/{id}` | Administrator | **Видалення конференц-залу** |
| `GET` | `/api/rooms` | будь-яка | Перелік активних залів |
| `GET` | `/api/rooms/{id}` | будь-яка | Зал за ідентифікатором |
| `POST` | `/api/rooms/{id}/amenities` | Administrator | Додати послугу або змінити її ціну |
| `DELETE` | `/api/rooms/{id}/amenities/{amenityId}` | Administrator | Прибрати послугу із залу |

### Бронювання

| Метод | Маршрут | Роль | Опис |
|---|---|---|---|
| `GET` | `/api/bookings/available-rooms` | будь-яка | **Пошук доступних залів** |
| `POST` | `/api/bookings` | будь-яка | **Бронювання залу** |
| `POST` | `/api/bookings/quote` | будь-яка | Розрахунок вартості без бронювання |
| `GET` | `/api/bookings/{id}` | будь-яка | Бронювання за ідентифікатором |
| `DELETE` | `/api/bookings/{id}` | будь-яка | Скасувати бронювання |

### Звіти

| Метод | Маршрут | Роль |
|---|---|---|
| `GET` | `/api/reports/summary` | Administrator |
| `GET` | `/api/reports/room-utilization` | Administrator |
| `GET` | `/api/reports/revenue` | Administrator |
| `GET` | `/api/reports/amenity-demand` | Administrator |
| `GET` | `/api/reports/hourly-load` | Administrator |
| `GET` | `/api/reports/pricing-bands` | Administrator |

### Приклади

**Додавання залу**

```http
POST /api/rooms
X-Api-Key: admin-demo-key-3f9a2c
Content-Type: application/json

{
  "name": "Зал А",
  "capacity": 50,
  "basePricePerHour": 2000,
  "amenities": [
    { "name": "Проєктор", "price": 500 },
    { "name": "Wi-Fi", "price": 300 }
  ]
}
```

```json
201 Created
{
  "id": "f4e6a6de-ba59-4287-8283-16b7b6aa41ff",
  "name": "Зал А",
  "message": "Зал «Зал А» успішно створено."
}
```

**Редагування залу** — передаються лише поля, що змінюються:

```http
PATCH /api/rooms/{id}
{ "basePricePerHour": 2500 }
```

**Пошук доступних залів**

```http
GET /api/bookings/available-rooms?date=2026-09-04&startTime=10:00&endTime=14:00&capacity=50
```

```json
[
  { "id": "...", "name": "Зал А", "capacity": 50, "basePricePerHour": 2000, "estimatedRoomCost": 8600 },
  { "id": "...", "name": "Зал B", "capacity": 100, "basePricePerHour": 3500, "estimatedRoomCost": 15050 }
]
```

Разом із залом повертається орієнтовна вартість саме на цей проміжок — щоб зали
можна було одразу порівняти за ціною, без окремого запиту на кожен.

**Бронювання залу**

```http
POST /api/bookings
{
  "roomId": "f4e6a6de-...",
  "date": "2026-09-04",
  "startTime": "10:00",
  "durationMinutes": 240,
  "attendees": 45,
  "customerName": "ТОВ «Приклад»",
  "customerEmail": "office@example.com",
  "amenityIds": ["8a31..."]
}
```

```json
201 Created
{
  "id": "...",
  "roomName": "Зал А",
  "date": "2026-09-04",
  "startTime": "10:00:00",
  "endTime": "14:00:00",
  "status": "Confirmed",
  "cost": {
    "roomCost": 8600.00,
    "amenitiesCost": 500.00,
    "total": 9100.00,
    "segments": [
      { "band": "Стандартні години", "from": "10:00:00", "to": "12:00:00", "hours": 2, "multiplier": 1.00, "amount": 4000.00 },
      { "band": "Пікові години",     "from": "12:00:00", "to": "14:00:00", "hours": 2, "multiplier": 1.15, "amount": 4600.00 }
    ]
  }
}
```

### Коди помилок

Усі помилки повертаються у форматі [RFC 7807 Problem Details](https://www.rfc-editor.org/rfc/rfc7807)
з машиночитним полем `code` і `traceId` для звернення в підтримку.

| Код | Коли |
|---|---|
| `400` | Вхідні дані не пройшли валідацію (`validation_failed`) |
| `401` | Ключ відсутній або невідомий |
| `403` | Ключа недостатньо для цієї операції |
| `404` | Зал або бронювання не знайдено |
| `409` | Час зайнято, назва зайнята, бронювання вже скасовано |
| `422` | Порушено бізнес-правило: місткість, робочі години, дата в минулому |
| `429` | Перевищено ліміт запитів |

```json
409 Conflict
{
  "title": "Конфлікт",
  "status": 409,
  "detail": "Зал «Зал А» вже заброньовано на 04.09.2026 10:00–14:00.",
  "code": "time_slot_taken",
  "traceId": "00-fe42742681cfb96d-..."
}
```

---

## Розрахунок вартості оренди

```
Загальна вартість = вартість залу за тарифними смугами + разові платежі за послуги
```

Період бронювання **ріжеться на ділянки по межах тарифних смуг**, і кожна ділянка
оцінюється за власною ставкою. Тому бронювання, що перетинає кілька смуг, рахується коректно.

| Смуга | Години | Коефіцієнт | Пріоритет |
|---|---|---|---|
| Ранкові години | 06:00–09:00 | ×0.90 (−10%) | 50 |
| Стандартні години | 09:00–18:00 | ×1.00 | 10 |
| Пікові години | 12:00–14:00 | ×1.15 (+15%) | **100** |
| Вечірні години | 18:00–23:00 | ×0.80 (−20%) | 50 |

Пікові години лежать **усередині** стандартних. Конфлікт вирішується пріоритетом:
смуга з вищим пріоритетом перекриває нижчу, тож 12:00–14:00 завжди рахується з націнкою.

**Приклад.** «Зал А» (2000 грн/год), 10:00–14:00, проєктор (500 грн):

| Ділянка | Смуга | Розрахунок | Сума |
|---|---|---|---|
| 10:00–12:00 | Стандартні | 2000 × 1.00 × 2 год | 4000 грн |
| 12:00–14:00 | Пікові | 2000 × 1.15 × 2 год | 4600 грн |
| | | **оренда залу** | **8600 грн** |
| | Проєктор | разовий платіж | 500 грн |
| | | **РАЗОМ** | **9100 грн** |

Деталізація (`cost.segments`) повертається клієнту завжди — вартість має бути прозорою.

**Робочі години закладу** — 06:00–23:00 (об'єднання тарифних смуг). Бронювання поза
цими межами відхиляється з кодом `outside_working_hours`.

---

## Звіти

Кожен звіт відповідає на конкретне управлінське питання, а не просто вивантажує дані.
Усі приймають `?from=&to=` (за замовчуванням — останні 30 днів).

| Звіт | На яке питання відповідає |
|---|---|
| `summary` | Скільки заробили, який середній чек, яка частка зривів бронювань |
| `room-utilization` | Який зал недозавантажений, а який витримає підвищення ціни. Показує ще й середню заповненість — чи не продають великий зал під маленькі зустрічі |
| `revenue` | Динаміка виторгу за днями / тижнями / місяцями — сезонність і планування |
| `amenity-demand` | Що реально купують: які послуги докупити в інші зали, а які не окуповуються |
| `hourly-load` | Чи збігаються «пікові години» з тарифів із реальним піком попиту |
| `pricing-bands` | Скільки коштували знижки і скільки принесла націнка — у гривнях |

Звіт `pricing-bands` розкладає виторг за тарифними смугами й порівнює його з базовим
тарифом. Базова ставка **виводиться із зафіксованої вартості бронювання**, а не читається
з поточної ціни залу, — тож звіт не «попливе» після зміни прайсу.

```json
GET /api/reports/pricing-bands
{
  "totalRoomRevenue": 12600.00,
  "totalDiscountOrSurcharge": 600.00,
  "bands": [
    { "band": "Стандартні години", "bookedHours": 4, "revenue": 8000.00, "revenueAtBaseRate": 8000.00, "discountOrSurcharge": 0.00 },
    { "band": "Пікові години",     "bookedHours": 2, "revenue": 4600.00, "revenueAtBaseRate": 4000.00, "discountOrSurcharge": 600.00 }
  ]
}
```

---

## Архітектура

Чотиришарова архітектура з залежностями, спрямованими до центру: домен нічого не знає
про EF Core, ASP.NET Core чи формат JSON.

```
ConferenceBooking.Api              HTTP: контролери, Swagger, автентифікація, обробка помилок
        ↓
ConferenceBooking.Infrastructure   EF Core, репозиторії, міграції, сідинг
        ↓
ConferenceBooking.Application      Сценарії, DTO, валідатори, звіти
        ↓
ConferenceBooking.Domain           Сутності, об'єкти-значення, бізнес-правила, розрахунок вартості
```

```
src/
  ConferenceBooking.Domain/
    Common/          Entity, Guard, DomainException, NameNormalizer, IUnitOfWork
    Rooms/           ConferenceRoom (корінь агрегату), Amenity, RoomAmenity
    Bookings/        Booking (корінь агрегату), BookingPeriod, BookingAmenity
    Pricing/         PricingPolicy, PricingBand, RentalCostCalculator
  ConferenceBooking.Application/
    Rooms/           RoomAppService, AmenityCatalog, валідатори, DTO
    Bookings/        BookingAppService, BookingPeriodPolicy, валідатори, DTO
    Reports/         ReportAppService, DTO звітів
    Configuration/   PricingOptions, BookingPolicyOptions
  ConferenceBooking.Infrastructure/
    Persistence/     DbContext, конфігурації, репозиторії, UnitOfWork, міграції, сідинг
  ConferenceBooking.Api/
    Controllers/     RoomsController, BookingsController, ReportsController
    Security/        Автентифікація за API-ключем, ролі, політики
    Middleware/      Обробка винятків, заголовки безпеки
    Filters/         ValidationFilter
tests/
  ConferenceBooking.UnitTests/     65 тестів
```

Детальніший розбір ухвалених рішень — у [docs/DOCUMENTATION.md](docs/DOCUMENTATION.md).

---

## Безпека

| Загроза | Захист |
|---|---|
| Неавторизований доступ | API-ключі + ролі; **fallback-політика** вимагає автентифікації на всіх маршрутах, тож забутий `[Authorize]` не відкриває доступ |
| Підбір ключа за часом відповіді | Порівняння хешів ключів за сталий час (`CryptographicOperations.FixedTimeEquals`) |
| Перебір і DoS | Rate limiting: 120 запитів/хв на ключ або IP → `429` |
| Витік деталей реалізації | Стектрейси лише в Development; у продакшені — узагальнене повідомлення + `traceId` |
| SQL-ін'єкції | Тільки параметризовані запити EF Core |
| Некоректні дані | FluentValidation на межі + інваріанти в доменних сутностях |
| Подвійне бронювання | Перевірка зайнятості та вставка в одній транзакції з рівнем `Serializable` |
| MIME-sniffing, clickjacking | `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`; заголовки `Server`/`X-Powered-By` прибрано |
| Небажаний доступ з браузера | CORS закритий за замовчуванням; походження задаються явно в `Cors:AllowedOrigins` |
| Втрата даних при видаленні залу | М'яке видалення + заборона видаляти зал з активними бронями |

---

## Конфігурація

`src/ConferenceBooking.Api/appsettings.json`:

| Секція | Призначення |
|---|---|
| `ConnectionStrings:Default` | Рядок підключення до SQLite |
| `Venue:TimeZoneId` | Часовий пояс закладу (за замовчуванням `FLE Standard Time`, Київ) |
| `Pricing:Bands` | **Тарифна сітка**: назва, межі, коефіцієнт, пріоритет |
| `BookingPolicy` | Глибина бронювання наперед, крок сітки часу, ліміт учасників |
| `ApiKeys` | Заголовок і перелік виданих ключів із ролями |
| `Cors:AllowedOrigins` | Дозволені походження для браузерних клієнтів |

Змінити знижку, зсунути пікові години або додати нову тарифну смугу можна
**без правок коду** — достатньо відредагувати `Pricing:Bands`. Конфігурація
перевіряється на старті застосунку: суперечлива сітка не дасть йому піднятися.

### Перехід на PostgreSQL

Замінити провайдер у [`DependencyInjection.cs`](src/ConferenceBooking.Infrastructure/DependencyInjection.cs)
(`UseSqlite` → `UseNpgsql`), оновити рядок підключення та перегенерувати міграції.
Решта коду змін не потребує — доменний і прикладний шари про СУБД не знають.

---

## Тести

```bash
dotnet test
```

**65 тестів**, усі проходять:

| Набір | Що покриває |
|---|---|
| `RentalCostCalculatorTests` | Усі тарифні смуги, перетин смуг, пріоритет піку, півгодинні слоти, послуги, час поза робочими годинами |
| `BookingPeriodTests` | Інваріанти періоду, перехід через опівніч, виявлення перетинів |
| `ConferenceRoomTests` | Інваріанти залу, послуги, місткість |
| `BookingPeriodPolicyTests` | Робочі години, сітка часу, минулі та надто далекі дати |
| `BookingAppServiceTests` | Наскрізні сценарії: розрахунок, подвійне бронювання, суміжні слоти, скасування, пошук |
| `RoomAppServiceTests` | Створення, часткове редагування, каталог послуг, м'яке видалення |
| `ReportAppServiceTests` | Коректність усіх звітів і зведення сум |

Сервісні тести працюють поверх **реального SQLite у пам'яті**, а не InMemory-провайдера
EF Core: InMemory не має ні транзакцій, ні обмежень цілісності, тож саме ті помилки,
які ці тести мають ловити, він би пропустив.

---

## Початкові дані

Створюються автоматично під час першого запуску (ідемпотентно).

**Зали**

| Назва | Місткість | Базова вартість |
|---|---|---|
| Зал А | 50 осіб | 2000 грн/год |
| Зал B | 100 осіб | 3500 грн/год |
| Зал C | 30 осіб | 1500 грн/год |

**Послуги** (доступні в усіх залах)

| Назва | Ціна |
|---|---|
| Проєктор | 500 грн |
| Wi-Fi | 300 грн |
| Звук | 700 грн |

---

## Технології

.NET 9 · ASP.NET Core Web API · Entity Framework Core 9 · SQLite ·
FluentValidation · Swashbuckle (OpenAPI/Swagger) · xUnit
