using ConferenceBooking.Application.Common;
using ConferenceBooking.Domain.Bookings;
using ConferenceBooking.Domain.Common;
using ConferenceBooking.Domain.Rooms;
using ConferenceBooking.Infrastructure.Persistence;
using ConferenceBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConferenceBooking.Infrastructure;

/// <summary>Реєстрація інфраструктурного шару в контейнері залежностей.</summary>
public static class DependencyInjection
{
    /// <summary>Часовий пояс закладу за замовчуванням.</summary>
    public const string DefaultVenueTimeZone = "FLE Standard Time";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string? venueTimeZoneId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<ConferenceBookingDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IConferenceRoomRepository, ConferenceRoomRepository>();
        services.AddScoped<IAmenityRepository, AmenityRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<DatabaseSeeder>();

        services.AddSingleton<IDateTimeProvider>(
            new SystemDateTimeProvider(ResolveTimeZone(venueTimeZoneId)));

        return services;
    }

    /// <summary>
    /// Знаходить часовий пояс закладу. Ідентифікатори поясів різні у Windows і Linux,
    /// тому невідоме значення не валить застосунок, а відкочується на пояс сервера.
    /// </summary>
    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            timeZoneId = DefaultVenueTimeZone;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }
}
