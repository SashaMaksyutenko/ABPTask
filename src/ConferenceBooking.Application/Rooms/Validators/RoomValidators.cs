using ConferenceBooking.Application.Rooms.Dtos;
using ConferenceBooking.Domain.Common;
using ConferenceBooking.Domain.Rooms;
using FluentValidation;

namespace ConferenceBooking.Application.Rooms.Validators;

/// <summary>
/// Валідація послуги у складі запиту.
/// Валідатори — перший рубіж: вони відсікають явно некоректний ввід ще до звернення до БД
/// і повертають клієнту всі помилки одразу, а не по одній.
/// </summary>
public sealed class AmenityInputValidator : AbstractValidator<AmenityInput>
{
    public AmenityInputValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Назва послуги є обов'язковою.")
            .MaximumLength(Amenity.MaxNameLength);

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Ціна послуги не може бути від'ємною.")
            .LessThanOrEqualTo(Guard.MaxMoney);
    }
}

public sealed class CreateRoomRequestValidator : AbstractValidator<CreateRoomRequest>
{
    public CreateRoomRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Назва залу є обов'язковою.")
            .MaximumLength(ConferenceRoom.MaxNameLength);

        RuleFor(x => x.Capacity)
            .GreaterThan(0).WithMessage("Місткість має бути більшою за нуль.")
            .LessThanOrEqualTo(ConferenceRoom.MaxCapacity);

        RuleFor(x => x.BasePricePerHour)
            .GreaterThanOrEqualTo(0).WithMessage("Базова вартість не може бути від'ємною.")
            .LessThanOrEqualTo(Guard.MaxMoney);

        RuleForEach(x => x.Amenities).SetValidator(new AmenityInputValidator());

        RuleFor(x => x.Amenities)
            .Must(HaveUniqueNames)
            .When(x => x.Amenities is not null)
            .WithMessage("Назви послуг у межах залу мають бути унікальними.");
    }

    internal static bool HaveUniqueNames(IReadOnlyList<AmenityInput>? amenities) =>
        amenities is null ||
        amenities
            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
            .Select(a => a.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == amenities.Count;
}

public sealed class UpdateRoomRequestValidator : AbstractValidator<UpdateRoomRequest>
{
    public UpdateRoomRequestValidator()
    {
        RuleFor(x => x)
            .Must(HasAnyChange)
            .WithMessage("Запит на оновлення не містить жодного поля для зміни.");

        RuleFor(x => x.Name!)
            .NotEmpty().MaximumLength(ConferenceRoom.MaxNameLength)
            .When(x => x.Name is not null);

        RuleFor(x => x.Capacity!.Value)
            .GreaterThan(0).LessThanOrEqualTo(ConferenceRoom.MaxCapacity)
            .When(x => x.Capacity.HasValue);

        RuleFor(x => x.BasePricePerHour!.Value)
            .GreaterThanOrEqualTo(0).LessThanOrEqualTo(Guard.MaxMoney)
            .When(x => x.BasePricePerHour.HasValue);

        RuleForEach(x => x.Amenities).SetValidator(new AmenityInputValidator());

        RuleFor(x => x.Amenities)
            .Must(CreateRoomRequestValidator.HaveUniqueNames)
            .When(x => x.Amenities is not null)
            .WithMessage("Назви послуг у межах залу мають бути унікальними.");
    }

    private static bool HasAnyChange(UpdateRoomRequest request) =>
        request.Name is not null ||
        request.Capacity.HasValue ||
        request.BasePricePerHour.HasValue ||
        request.Amenities is not null;
}
