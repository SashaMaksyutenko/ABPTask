using ConferenceBooking.Application.Bookings;
using ConferenceBooking.Application.Reports;
using ConferenceBooking.Application.Rooms;
using ConferenceBooking.Domain.Pricing;
using ConferenceBooking.Infrastructure.Persistence;
using ConferenceBooking.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConferenceBooking.UnitTests.Application;

/// <summary>
/// Зібраний застосунок для тестів поверх SQLite у пам'яті.
///
/// Використовується справжній провайдер SQLite, а не InMemory-провайдер EF Core:
/// InMemory не має ані транзакцій, ані обмежень цілісності, тому саме ті помилки,
/// які ці тести мають ловити, він би пропустив.
/// </summary>
internal sealed class TestApplication : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private TestApplication(SqliteConnection connection, ConferenceBookingDbContext db, DateTime now)
    {
        _connection = connection;
        Db = db;

        var policy = TestData.Policy();
        var calculator = new RentalCostCalculator(policy);
        var unitOfWork = new UnitOfWork(db);
        var roomRepository = new ConferenceRoomRepository(db);
        var bookingRepository = new BookingRepository(db);
        var amenityRepository = new AmenityRepository(db);
        var clock = new FixedDateTimeProvider(now);

        Rooms = new RoomAppService(
            roomRepository,
            bookingRepository,
            new AmenityCatalog(amenityRepository),
            unitOfWork,
            clock,
            NullLogger<RoomAppService>.Instance);

        Bookings = new BookingAppService(
            roomRepository,
            bookingRepository,
            calculator,
            new BookingPeriodPolicy(policy, TestData.BookingPolicy(), clock),
            TestData.BookingPolicy(),
            unitOfWork,
            NullLogger<BookingAppService>.Instance);

        Reports = new ReportAppService(bookingRepository, roomRepository, calculator, policy);
    }

    public ConferenceBookingDbContext Db { get; }

    public IRoomAppService Rooms { get; }

    public IBookingAppService Bookings { get; }

    public IReportAppService Reports { get; }

    /// <summary>Створює порожню базу зі схемою застосунку.</summary>
    public static async Task<TestApplication> CreateAsync(DateTime now)
    {
        // З'єднання тримається відкритим увесь час життя тесту: закриття останнього
        // з'єднання знищує базу «:memory:» разом зі схемою й даними.
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ConferenceBookingDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new ConferenceBookingDbContext(options);
        await db.Database.EnsureCreatedAsync();

        return new TestApplication(connection, db, now);
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
