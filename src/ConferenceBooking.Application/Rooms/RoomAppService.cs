using ConferenceBooking.Application.Common;
using ConferenceBooking.Application.Rooms.Dtos;
using ConferenceBooking.Domain.Bookings;
using ConferenceBooking.Domain.Common;
using ConferenceBooking.Domain.Rooms;
using Microsoft.Extensions.Logging;

namespace ConferenceBooking.Application.Rooms;

/// <summary>Сценарії роботи з конференц-залами.</summary>
public interface IRoomAppService
{
    Task<RoomCreatedResponse> CreateAsync(CreateRoomRequest request, CancellationToken cancellationToken = default);

    Task<RoomResponse> UpdateAsync(Guid roomId, UpdateRoomRequest request, CancellationToken cancellationToken = default);

    Task<OperationResultResponse> DeleteAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task<RoomResponse> GetAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoomResponse>> ListAsync(CancellationToken cancellationToken = default);

    Task<RoomResponse> AddAmenityAsync(Guid roomId, AmenityInput amenity, CancellationToken cancellationToken = default);

    Task<RoomResponse> RemoveAmenityAsync(Guid roomId, Guid amenityId, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IRoomAppService"/>
public sealed class RoomAppService : IRoomAppService
{
    private readonly IConferenceRoomRepository _rooms;
    private readonly IBookingRepository _bookings;
    private readonly IAmenityCatalog _amenityCatalog;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<RoomAppService> _logger;

    public RoomAppService(
        IConferenceRoomRepository rooms,
        IBookingRepository bookings,
        IAmenityCatalog amenityCatalog,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ILogger<RoomAppService> logger)
    {
        _rooms = rooms;
        _bookings = bookings;
        _amenityCatalog = amenityCatalog;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<RoomCreatedResponse> CreateAsync(
        CreateRoomRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await EnsureNameIsFreeAsync(request.Name, excludeRoomId: null, cancellationToken).ConfigureAwait(false);

        var room = new ConferenceRoom(request.Name, request.Capacity, request.BasePricePerHour);

        var amenities = await _amenityCatalog
            .ResolveAsync(request.Amenities ?? [], cancellationToken)
            .ConfigureAwait(false);

        room.ReplaceAmenities(amenities);

        _rooms.Add(room);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Створено зал {RoomId} «{RoomName}»", room.Id, room.Name);

        return new RoomCreatedResponse(room.Id, room.Name, $"Зал «{room.Name}» успішно створено.");
    }

    public async Task<RoomResponse> UpdateAsync(
        Guid roomId,
        UpdateRoomRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var room = await LoadRoomAsync(roomId, cancellationToken).ConfigureAwait(false);

        if (request.Name is not null)
        {
            await EnsureNameIsFreeAsync(request.Name, roomId, cancellationToken).ConfigureAwait(false);
            room.Rename(request.Name);
        }

        if (request.Capacity is { } capacity)
        {
            room.ChangeCapacity(capacity);
        }

        if (request.BasePricePerHour is { } price)
        {
            room.ChangeBasePrice(price);
        }

        if (request.Amenities is not null)
        {
            var amenities = await _amenityCatalog
                .ResolveAsync(request.Amenities, cancellationToken)
                .ConfigureAwait(false);

            room.ReplaceAmenities(amenities);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Оновлено зал {RoomId}", room.Id);

        return room.ToResponse();
    }

    public async Task<OperationResultResponse> DeleteAsync(
        Guid roomId,
        CancellationToken cancellationToken = default)
    {
        var room = await LoadRoomAsync(roomId, cancellationToken).ConfigureAwait(false);

        // Зал із проданими майбутніми бронями не видаляємо: тихе зникнення залу
        // означало б зірвані заходи в клієнтів, які вже все оплатили й запросили людей.
        var hasFutureBookings = await _bookings
            .HasConfirmedBookingsFromAsync(roomId, _clock.Today, cancellationToken)
            .ConfigureAwait(false);

        if (hasFutureBookings)
        {
            throw new ConflictException(
                "room_has_active_bookings",
                $"Зал «{room.Name}» має активні бронювання на майбутні дати. " +
                "Спочатку скасуйте їх, після чого зал можна буде видалити.");
        }

        room.MarkDeleted();
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Видалено зал {RoomId} «{RoomName}»", room.Id, room.Name);

        return new OperationResultResponse(room.Id, $"Зал «{room.Name}» видалено.");
    }

    public async Task<RoomResponse> GetAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var room = await LoadRoomAsync(roomId, cancellationToken).ConfigureAwait(false);
        return room.ToResponse();
    }

    public async Task<IReadOnlyList<RoomResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rooms = await _rooms.ListAsync(cancellationToken).ConfigureAwait(false);
        return rooms.Select(RoomMapper.ToResponse).ToArray();
    }

    public async Task<RoomResponse> AddAmenityAsync(
        Guid roomId,
        AmenityInput amenity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(amenity);

        var room = await LoadRoomAsync(roomId, cancellationToken).ConfigureAwait(false);

        var resolved = await _amenityCatalog.ResolveAsync([amenity], cancellationToken).ConfigureAwait(false);
        var (catalogEntry, price) = resolved[0];

        room.AddOrUpdateAmenity(catalogEntry, price);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("До залу {RoomId} додано послугу «{Amenity}»", room.Id, catalogEntry.Name);

        return room.ToResponse();
    }

    public async Task<RoomResponse> RemoveAmenityAsync(
        Guid roomId,
        Guid amenityId,
        CancellationToken cancellationToken = default)
    {
        var room = await LoadRoomAsync(roomId, cancellationToken).ConfigureAwait(false);

        if (!room.RemoveAmenity(amenityId))
        {
            throw new EntityNotFoundException($"Послуга залу «{room.Name}»", amenityId);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return room.ToResponse();
    }

    private async Task<ConferenceRoom> LoadRoomAsync(Guid roomId, CancellationToken cancellationToken) =>
        await _rooms.GetByIdAsync(roomId, cancellationToken).ConfigureAwait(false)
        ?? throw new EntityNotFoundException("Конференц-зал", roomId);

    private async Task EnsureNameIsFreeAsync(string name, Guid? excludeRoomId, CancellationToken cancellationToken)
    {
        var taken = await _rooms
            .NameExistsAsync(name.Trim(), excludeRoomId, cancellationToken)
            .ConfigureAwait(false);

        if (taken)
        {
            throw new ConflictException(
                "room_name_taken",
                $"Зал із назвою «{name.Trim()}» уже існує.");
        }
    }
}
