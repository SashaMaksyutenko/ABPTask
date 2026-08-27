using ConferenceBooking.Application.Bookings;
using ConferenceBooking.Application.Configuration;
using ConferenceBooking.Application.Reports;
using ConferenceBooking.Application.Rooms;
using ConferenceBooking.Application.Rooms.Validators;
using ConferenceBooking.Domain.Pricing;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ConferenceBooking.Application;

/// <summary>Реєстрація прикладного шару в контейнері залежностей.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Підключає сценарії, валідатори та розрахунок вартості.
    /// Тарифна політика приходить готовою з конфігурації — так її валідність перевіряється
    /// один раз на старті застосунку, а не на кожному запиті.
    /// </summary>
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        PricingOptions pricingOptions,
        BookingPolicyOptions bookingPolicyOptions)
    {
        ArgumentNullException.ThrowIfNull(pricingOptions);
        ArgumentNullException.ThrowIfNull(bookingPolicyOptions);

        var policy = pricingOptions.ToPolicy();

        services.AddSingleton(policy);
        services.AddSingleton(bookingPolicyOptions);
        services.AddSingleton<IRentalCostCalculator>(_ => new RentalCostCalculator(policy));

        services.AddScoped<BookingPeriodPolicy>();
        services.AddScoped<IAmenityCatalog, AmenityCatalog>();
        services.AddScoped<IRoomAppService, RoomAppService>();
        services.AddScoped<IBookingAppService, BookingAppService>();
        services.AddScoped<IReportAppService, ReportAppService>();

        // Валідатори не мають стану, тож реєструються синглтонами — це прибирає зайві алокації
        // на кожному запиті. Знайти їх скануванням складання дешевше, ніж підтримувати
        // список реєстрацій, який неминуче відстане від нових контрактів.
        services.AddValidatorsFromAssemblyContaining<CreateRoomRequestValidator>(ServiceLifetime.Singleton);

        return services;
    }
}
