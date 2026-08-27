using ConferenceBooking.Application.Bookings.Dtos;
using ConferenceBooking.Application.Common;
using ConferenceBooking.Application.Configuration;
using ConferenceBooking.Application.Rooms;
using ConferenceBooking.Domain.Bookings;
using ConferenceBooking.Domain.Common;
using ConferenceBooking.Domain.Pricing;
using ConferenceBooking.Domain.Rooms;
using Microsoft.Extensions.Logging;

namespace ConferenceBooking.Application.Bookings;

/// <summary>Сценарії пошуку вільних залів і бронювання.</summary>
public interface IBookingAppService
{
    Task<IReadOnlyList<AvailableRoomResponse>> FindAvailableRoomsAsync(
        AvailabilitySearchRequest request,
        CancellationToken cancellationToken = default);

    Task<BookingResponse> CreateAsync(CreateBookingRequest request, CancellationToken cancellationToken = default);

    Task<CostBreakdownResponse> QuoteAsync(QuoteRequest request, CancellationToken cancellationToken = default);

    Task<BookingResponse> GetAsync(Guid bookingId, CancellationToken cancellationToken = default);

    Task<Rooms.Dtos.OperationResultResponse> CancelAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IBookingAppService"/>
public sealed class BookingAppService : IBookingAppService
{
    private readonly IConferenceRoomRepository _rooms;
    private readonly IBookingRepository _bookings;
    private readonly IRentalCostCalculator _calculator;
    private readonly BookingPeriodPolicy _periodPolicy;
    private readonly BookingPolicyOptions _options;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BookingAppService> _logger;

    public BookingAppService(
        IConferenceRoomRepository rooms,
        IBookingRepository bookings,
        IRentalCostCalculator calculator,
        BookingPeriodPolicy periodPolicy,
        BookingPolicyOptions options,
        IUnitOfWork unitOfWork,
        ILogger<BookingAppService> logger)
    {
        _rooms = rooms;
        _bookings = bookings;
        _calculator = calculator;
        _periodPolicy = periodPolicy;
        _options = options;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AvailableRoomResponse>> FindAvailableRoomsAsync(
        AvailabilitySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureAttendeesWithinLimit(request.Capacity);

        var period = BookingPeriod.Create(request.Date, request.StartTime, request.EndTime);
        _periodPolicy.EnsureSearchable(period);

        var rooms = await _rooms
            .FindAvailableAsync(period, request.Capacity, cancellationToken)
            .ConfigureAwait(false);

        return rooms
            .Select(room => new AvailableRoomResponse(
                room.Id,
                room.Name,
                room.Capacity,
                room.BasePricePerHour,
                room.Amenities.Select(RoomMapper.ToResponse).OrderBy(a => a.Name).ToArray(),
                _calculator.Calculate(room.BasePricePerHour, period, []).RoomCost))
            .OrderBy(room => room.EstimatedRoomCost)
            .ToArray();
    }

    public async Task<CostBreakdownResponse> QuoteAsync(
        QuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var period = BookingPeriod.FromDuration(
            request.Date,
            request.StartTime,
            TimeSpan.FromMinutes(request.DurationMinutes));

        _periodPolicy.EnsureSearchable(period);

        var room = await LoadRoomAsync(request.RoomId, cancellationToken).ConfigureAwait(false);
        var charges = ResolveAmenityCharges(room, request.AmenityIds);

        return _calculator.Calculate(room.BasePricePerHour, period, charges).ToResponse();
    }

    public async Task<BookingResponse> CreateAsync(
        CreateBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureAttendeesWithinLimit(request.Attendees);

        var period = BookingPeriod.FromDuration(
            request.Date,
            request.StartTime,
            TimeSpan.FromMinutes(request.DurationMinutes));

        _periodPolicy.EnsureBookable(period);

        // Перевірка вільного часу і вставка бронювання виконуються в одній транзакції:
        // без цього два одночасні запити на той самий слот могли б обидва пройти перевірку
        // й обидва створити бронювання на один зал.
        var booking = await _unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var room = await LoadRoomAsync(request.RoomId, ct).ConfigureAwait(false);

                if (!room.CanAccommodate(request.Attendees))
                {
                    throw new DomainException(
                        "capacity_exceeded",
                        $"Зал «{room.Name}» вміщує {room.Capacity} осіб, запитано {request.Attendees}.");
                }

                var occupied = await _bookings.HasOverlapAsync(room.Id, period, ct).ConfigureAwait(false);
                if (occupied)
                {
                    throw new ConflictException(
                        "time_slot_taken",
                        $"Зал «{room.Name}» вже заброньовано на {period}.");
                }

                var charges = ResolveAmenityCharges(room, request.AmenityIds);
                var cost = _calculator.Calculate(room.BasePricePerHour, period, charges);

                var created = new Booking(
                    room.Id,
                    room.Name,
                    period,
                    request.CustomerName,
                    request.CustomerEmail,
                    request.Attendees,
                    cost);

                _bookings.Add(created);
                await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

                return created;
            },
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Створено бронювання {BookingId} залу {RoomId} на {Period} на суму {Total} грн",
            booking.Id, booking.RoomId, period, booking.TotalCost);

        return booking.ToResponse(RestoreSegments(booking));
    }

    public async Task<BookingResponse> GetAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await LoadBookingAsync(bookingId, cancellationToken).ConfigureAwait(false);
        return booking.ToResponse(RestoreSegments(booking));
    }

    public async Task<Rooms.Dtos.OperationResultResponse> CancelAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await LoadBookingAsync(bookingId, cancellationToken).ConfigureAwait(false);

        booking.Cancel();
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Скасовано бронювання {BookingId}", booking.Id);

        return new Rooms.Dtos.OperationResultResponse(
            booking.Id,
            $"Бронювання залу «{booking.RoomName}» на {booking.Date:dd.MM.yyyy} скасовано.");
    }

    /// <summary>
    /// Відновлює деталізацію за тарифними смугами для вже збереженого бронювання.
    /// Базова ставка береться не з залу (вона могла змінитися), а виводиться із зафіксованої
    /// вартості оренди — тож деталізація завжди сходиться із сумою в рахунку.
    /// </summary>
    private IReadOnlyList<CostSegmentResponse> RestoreSegments(Booking booking)
    {
        var reference = _calculator.Calculate(1m, booking.Period, []);
        if (reference.RoomCost == 0m)
        {
            return [];
        }

        var effectiveBaseRate = booking.RoomCost / reference.RoomCost;

        return _calculator
            .Calculate(effectiveBaseRate, booking.Period, [])
            .Segments
            .Select(s => new CostSegmentResponse(s.BandName, s.From, s.To, s.Hours, s.Multiplier, s.Amount))
            .ToArray();
    }

    private static IReadOnlyCollection<AmenityCharge> ResolveAmenityCharges(
        ConferenceRoom room,
        IReadOnlyList<Guid>? amenityIds)
    {
        if (amenityIds is null || amenityIds.Count == 0)
        {
            return [];
        }

        return room.ResolveAmenities(amenityIds)
            .Select(a => new AmenityCharge(a.AmenityId, a.Amenity.Name, a.Price))
            .ToArray();
    }

    private void EnsureAttendeesWithinLimit(int attendees)
    {
        if (attendees > _options.MaxAttendees)
        {
            throw new DomainException(
                "invalid_attendees",
                $"Максимальна кількість учасників — {_options.MaxAttendees}.");
        }
    }

    private async Task<ConferenceRoom> LoadRoomAsync(Guid roomId, CancellationToken cancellationToken) =>
        await _rooms.GetByIdAsync(roomId, cancellationToken).ConfigureAwait(false)
        ?? throw new EntityNotFoundException("Конференц-зал", roomId);

    private async Task<Booking> LoadBookingAsync(Guid bookingId, CancellationToken cancellationToken) =>
        await _bookings.GetByIdAsync(bookingId, cancellationToken).ConfigureAwait(false)
        ?? throw new EntityNotFoundException("Бронювання", bookingId);
}
