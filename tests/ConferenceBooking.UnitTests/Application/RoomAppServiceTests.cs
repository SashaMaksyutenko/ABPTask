using ConferenceBooking.Application.Bookings.Dtos;
using ConferenceBooking.Application.Rooms.Dtos;
using ConferenceBooking.Domain.Common;

namespace ConferenceBooking.UnitTests.Application;

public sealed class RoomAppServiceTests
{
    private static readonly DateTime Now = new(2024, 9, 1, 8, 0, 0);
    private static readonly DateOnly BookingDate = new(2024, 9, 2);

    [Fact]
    public async Task Create_DuplicateName_Throws()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        await app.Rooms.CreateAsync(new CreateRoomRequest("Зал А", 50, 2000m, []));

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => app.Rooms.CreateAsync(new CreateRoomRequest("зал а", 30, 1500m, [])));

        Assert.Equal("room_name_taken", exception.Code);
    }

    [Fact]
    public async Task Update_ChangesOnlyProvidedFields()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var created = await app.Rooms.CreateAsync(new CreateRoomRequest(
            "Зал А", 50, 2000m, [new AmenityInput("Проєктор", 500m)]));

        // Приклад із ТЗ: змінюємо лише вартість оренди.
        var updated = await app.Rooms.UpdateAsync(created.Id, new UpdateRoomRequest(null, null, 2500m, null));

        Assert.Equal(2500m, updated.BasePricePerHour);
        Assert.Equal("Зал А", updated.Name);
        Assert.Equal(50, updated.Capacity);
        Assert.Single(updated.Amenities);
    }

    [Fact]
    public async Task AddAmenity_AppendsWithoutTouchingExistingOnes()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var created = await app.Rooms.CreateAsync(new CreateRoomRequest(
            "Зал А", 50, 2000m, [new AmenityInput("Проєктор", 500m)]));

        // Другий приклад із ТЗ: додаємо послугу «Звук» вартістю 700 грн.
        var updated = await app.Rooms.AddAmenityAsync(created.Id, new AmenityInput("Звук", 700m));

        Assert.Equal(2, updated.Amenities.Count);
        Assert.Equal(700m, updated.Amenities.Single(a => a.Name == "Звук").Price);
        Assert.Equal(500m, updated.Amenities.Single(a => a.Name == "Проєктор").Price);
    }

    [Fact]
    public async Task AddAmenity_ReusesCatalogEntryAcrossRooms()
    {
        await using var app = await TestApplication.CreateAsync(Now);

        var first = await app.Rooms.CreateAsync(new CreateRoomRequest(
            "Зал А", 50, 2000m, [new AmenityInput("Проєктор", 500m)]));

        var second = await app.Rooms.CreateAsync(new CreateRoomRequest(
            "Зал B", 100, 3500m, [new AmenityInput("проєктор", 800m)]));

        var roomA = await app.Rooms.GetAsync(first.Id);
        var roomB = await app.Rooms.GetAsync(second.Id);

        // Одна позиція каталогу, але власна ціна в кожному залі —
        // без цього аналітика за послугами розсипалася б на дублікати.
        Assert.Equal(roomA.Amenities.Single().AmenityId, roomB.Amenities.Single().AmenityId);
        Assert.Equal(500m, roomA.Amenities.Single().Price);
        Assert.Equal(800m, roomB.Amenities.Single().Price);
    }

    [Fact]
    public async Task Update_ReplacingAmenities_SwapsWholeList()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var created = await app.Rooms.CreateAsync(new CreateRoomRequest(
            "Зал А", 50, 2000m, [new AmenityInput("Проєктор", 500m), new AmenityInput("Wi-Fi", 300m)]));

        var updated = await app.Rooms.UpdateAsync(
            created.Id,
            new UpdateRoomRequest(null, null, null, [new AmenityInput("Звук", 700m)]));

        Assert.Equal("Звук", Assert.Single(updated.Amenities).Name);
    }

    [Fact]
    public async Task Delete_RoomWithFutureBookings_Throws()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var created = await app.Rooms.CreateAsync(new CreateRoomRequest("Зал А", 50, 2000m, []));

        await app.Bookings.CreateAsync(new CreateBookingRequest(
            created.Id, BookingDate, new TimeOnly(10, 0), 120, 10,
            "ТОВ «Приклад»", "office@example.com", null));

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => app.Rooms.DeleteAsync(created.Id));

        Assert.Equal("room_has_active_bookings", exception.Code);
    }

    [Fact]
    public async Task Delete_HidesRoomButKeepsBookingHistory()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var created = await app.Rooms.CreateAsync(new CreateRoomRequest("Зал А", 50, 2000m, []));

        var booking = await app.Bookings.CreateAsync(new CreateBookingRequest(
            created.Id, BookingDate, new TimeOnly(10, 0), 120, 10,
            "ТОВ «Приклад»", "office@example.com", null));

        await app.Bookings.CancelAsync(booking.Id);
        await app.Rooms.DeleteAsync(created.Id);

        Assert.Empty(await app.Rooms.ListAsync());

        // Історія бронювання лишається доступною — інакше зникла б і фінансова звітність.
        var stored = await app.Bookings.GetAsync(booking.Id);
        Assert.Equal("Зал А", stored.RoomName);
    }

    [Fact]
    public async Task Delete_FreesNameForANewRoom()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var created = await app.Rooms.CreateAsync(new CreateRoomRequest("Зал А", 50, 2000m, []));
        await app.Rooms.DeleteAsync(created.Id);

        var recreated = await app.Rooms.CreateAsync(new CreateRoomRequest("Зал А", 60, 2200m, []));

        Assert.NotEqual(created.Id, recreated.Id);
    }

    [Fact]
    public async Task Get_UnknownRoom_Throws()
    {
        await using var app = await TestApplication.CreateAsync(Now);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => app.Rooms.GetAsync(Guid.NewGuid()));
    }
}
