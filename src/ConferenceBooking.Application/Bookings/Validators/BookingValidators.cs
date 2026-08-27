using ConferenceBooking.Application.Bookings.Dtos;
using ConferenceBooking.Domain.Bookings;
using FluentValidation;

namespace ConferenceBooking.Application.Bookings.Validators;

public sealed class AvailabilitySearchRequestValidator : AbstractValidator<AvailabilitySearchRequest>
{
    public AvailabilitySearchRequestValidator()
    {
        RuleFor(x => x.EndTime)
            .GreaterThan(x => x.StartTime)
            .WithMessage("Час завершення має бути пізнішим за час початку.");

        RuleFor(x => x.Capacity)
            .GreaterThan(0).WithMessage("Місткість має бути більшою за нуль.");
    }
}

public sealed class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.RoomId).NotEmpty().WithMessage("Не вказано зал для бронювання.");

        RuleFor(x => x.DurationMinutes)
            .GreaterThanOrEqualTo((int)BookingPeriod.MinDuration.TotalMinutes)
            .LessThanOrEqualTo((int)BookingPeriod.MaxDuration.TotalMinutes)
            .WithMessage($"Тривалість має бути від {BookingPeriod.MinDuration.TotalMinutes:0} " +
                         $"до {BookingPeriod.MaxDuration.TotalMinutes:0} хвилин.");

        RuleFor(x => x.Attendees)
            .GreaterThan(0).WithMessage("Кількість учасників має бути більшою за нуль.");

        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Ім'я замовника є обов'язковим.")
            .MaximumLength(Booking.MaxCustomerNameLength);

        // Email — єдиний канал зв'язку із замовником, тому перевіряємо формат одразу,
        // а не з'ясовуємо про помилку в момент, коли треба надіслати підтвердження.
        RuleFor(x => x.CustomerEmail)
            .NotEmpty().WithMessage("Email замовника є обов'язковим.")
            .MaximumLength(Booking.MaxCustomerEmailLength)
            .EmailAddress().WithMessage("Email замовника має некоректний формат.");

        RuleFor(x => x.AmenityIds)
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Список послуг містить дублікати.");
    }
}

public sealed class QuoteRequestValidator : AbstractValidator<QuoteRequest>
{
    public QuoteRequestValidator()
    {
        RuleFor(x => x.RoomId).NotEmpty().WithMessage("Не вказано зал для розрахунку.");

        RuleFor(x => x.DurationMinutes)
            .GreaterThanOrEqualTo((int)BookingPeriod.MinDuration.TotalMinutes)
            .LessThanOrEqualTo((int)BookingPeriod.MaxDuration.TotalMinutes);

        RuleFor(x => x.AmenityIds)
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Список послуг містить дублікати.");
    }
}
