using ConferenceBooking.Application.Bookings.Dtos;
using ConferenceBooking.Application.Rooms.Dtos;
using ConferenceBooking.Domain.Common;

namespace ConferenceBooking.UnitTests.Application;

/// <summary>Наскрізні перевірки сценаріїв бронювання поверх реальної схеми БД.</summary>
public sealed class BookingAppServiceTests
{
    private static readonly DateTime Now = new(2024, 9, 1, 8, 0, 0);
    private static readonly DateOnly BookingDate = new(2024, 9, 2);

    [Fact]
    public async Task Create_CalculatesTotalAccordingToSpec()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var room = await CreateRoomAAsync(app);
        var projectorId = room.Amenities.Single(a => a.Name == "Проєктор").AmenityId;

        // 10:00–14:00 у «Залі А»: 2 год стандарту (4000) + 2 год піку (4600) + проєктор (500).
        var booking = await app.Bookings.CreateAsync(new CreateBookingRequest(
            room.Id, BookingDate, new TimeOnly(10, 0), 240, 45,
            "ТОВ «Приклад»", "office@example.com", [projectorId]));

        Assert.Equal(8600m, booking.Cost.RoomCost);
        Assert.Equal(500m, booking.Cost.AmenitiesCost);
        Assert.Equal(9100m, booking.Cost.Total);
        Assert.Equal("Confirmed", booking.Status);
        Assert.Equal(new TimeOnly(14, 0), booking.EndTime);
    }

    [Fact]
    public async Task Create_WhenSlotAlreadyTaken_Throws()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var room = await CreateRoomAAsync(app);

        await app.Bookings.CreateAsync(Request(room.Id, new TimeOnly(10, 0), 240));

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => app.Bookings.CreateAsync(Request(room.Id, new TimeOnly(12, 0), 120)));

        Assert.Equal("time_slot_taken", exception.Code);
    }

    [Fact]
    public async Task Create_BackToBackSlots_BothSucceed()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var room = await CreateRoomAAsync(app);

        await app.Bookings.CreateAsync(Request(room.Id, new TimeOnly(10, 0), 120));
        var second = await app.Bookings.CreateAsync(Request(room.Id, new TimeOnly(12, 0), 120));

        // Зал звільняється рівно о 12:00, тож суміжні бронювання конфліктувати не мають.
        Assert.Equal(new TimeOnly(12, 0), second.StartTime);
    }

    [Fact]
    public async Task Create_MoreAttendeesThanCapacity_Throws()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var room = await CreateRoomAAsync(app);

        var request = Request(room.Id, new TimeOnly(10, 0), 120) with { Attendees = 51 };

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => app.Bookings.CreateAsync(request));

        Assert.Equal("capacity_exceeded", exception.Code);
    }

    [Fact]
    public async Task Create_WithAmenityFromAnotherRoom_Throws()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var roomA = await CreateRoomAAsync(app);

        var roomBId = (await app.Rooms.CreateAsync(new CreateRoomRequest(
            "Зал B", 100, 3500m, [new AmenityInput("Караоке", 1200m)]))).Id;

        var roomB = await app.Rooms.GetAsync(roomBId);
        var karaokeId = roomB.Amenities.Single().AmenityId;

        var request = Request(roomA.Id, new TimeOnly(10, 0), 120) with { AmenityIds = [karaokeId] };

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => app.Bookings.CreateAsync(request));

        Assert.Equal("amenity_not_available", exception.Code);
    }

    [Fact]
    public async Task Cancel_FreesTheSlot()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var room = await CreateRoomAAsync(app);

        var booking = await app.Bookings.CreateAsync(Request(room.Id, new TimeOnly(10, 0), 120));
        await app.Bookings.CancelAsync(booking.Id);

        var rebooked = await app.Bookings.CreateAsync(Request(room.Id, new TimeOnly(10, 0), 120));

        Assert.NotEqual(booking.Id, rebooked.Id);
    }

    [Fact]
    public async Task Cancel_Twice_Throws()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var room = await CreateRoomAAsync(app);
        var booking = await app.Bookings.CreateAsync(Request(room.Id, new TimeOnly(10, 0), 120));

        await app.Bookings.CancelAsync(booking.Id);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => app.Bookings.CancelAsync(booking.Id));

        Assert.Equal("booking_already_cancelled", exception.Code);
    }

    [Fact]
    public async Task FindAvailableRooms_ExcludesBookedAndTooSmallRooms()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var roomA = await CreateRoomAAsync(app);

        await app.Rooms.CreateAsync(new CreateRoomRequest("Зал C", 30, 1500m, []));
        await app.Rooms.CreateAsync(new CreateRoomRequest("Зал B", 100, 3500m, []));

        await app.Bookings.CreateAsync(Request(roomA.Id, new TimeOnly(10, 0), 240));

        var available = await app.Bookings.FindAvailableRoomsAsync(
            new AvailabilitySearchRequest(BookingDate, new TimeOnly(10, 0), new TimeOnly(14, 0), 50));

        // «Зал C» замалий, «Зал А» зайнятий — лишається лише «Зал B».
        var single = Assert.Single(available);
        Assert.Equal("Зал B", single.Name);
        Assert.Equal(15_050m, single.EstimatedRoomCost);   // 3500 × (2 + 2×1.15)
    }

    [Fact]
    public async Task Quote_MatchesActualBookingCost()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var room = await CreateRoomAAsync(app);
        var projectorId = room.Amenities.Single(a => a.Name == "Проєктор").AmenityId;

        var quote = await app.Bookings.QuoteAsync(new QuoteRequest(
            room.Id, BookingDate, new TimeOnly(18, 0), 120, [projectorId]));

        var booking = await app.Bookings.CreateAsync(new CreateBookingRequest(
            room.Id, BookingDate, new TimeOnly(18, 0), 120, 10,
            "ТОВ «Приклад»", "office@example.com", [projectorId]));

        // Попередній розрахунок має точно збігатися з рахунком, інакше клієнт втратить довіру.
        Assert.Equal(quote.Total, booking.Cost.Total);
        Assert.Equal(3200m, quote.RoomCost);              // 2000 × 0.8 × 2 год
    }

    [Fact]
    public async Task Get_RestoresPricingSegmentsThatSumToStoredCost()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var room = await CreateRoomAAsync(app);
        var created = await app.Bookings.CreateAsync(Request(room.Id, new TimeOnly(10, 0), 240));

        // Ціна залу змінилася вже після бронювання.
        await app.Rooms.UpdateAsync(room.Id, new UpdateRoomRequest(null, null, 5000m, null));

        var reloaded = await app.Bookings.GetAsync(created.Id);

        Assert.Equal(8600m, reloaded.Cost.RoomCost);
        Assert.Equal(reloaded.Cost.RoomCost, reloaded.Cost.Segments.Sum(s => s.Amount));
    }

    private static async Task<RoomResponse> CreateRoomAAsync(TestApplication app)
    {
        var created = await app.Rooms.CreateAsync(new CreateRoomRequest(
            "Зал А", 50, 2000m,
            [
                new AmenityInput("Проєктор", 500m),
                new AmenityInput("Wi-Fi", 300m),
                new AmenityInput("Звук", 700m)
            ]));

        return await app.Rooms.GetAsync(created.Id);
    }

    private static CreateBookingRequest Request(Guid roomId, TimeOnly start, int durationMinutes) =>
        new(roomId, BookingDate, start, durationMinutes, 10,
            "ТОВ «Приклад»", "office@example.com", null);
}
