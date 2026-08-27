using ConferenceBooking.Application.Bookings.Dtos;
using ConferenceBooking.Application.Reports.Dtos;
using ConferenceBooking.Application.Rooms.Dtos;

namespace ConferenceBooking.UnitTests.Application;

public sealed class ReportAppServiceTests
{
    private static readonly DateTime Now = new(2024, 9, 1, 8, 0, 0);
    private static readonly DateOnly Day = new(2024, 9, 2);

    [Fact]
    public async Task Summary_CountsOnlyConfirmedBookingsInRevenue()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var room = await CreateRoomAsync(app);

        // 10:00–12:00 → 4000 грн, лишається підтвердженим.
        await app.Bookings.CreateAsync(Booking(room.Id, new TimeOnly(10, 0), 120));

        // 15:00–17:00 → 4000 грн, скасовується і не має потрапити у виторг.
        var cancelled = await app.Bookings.CreateAsync(Booking(room.Id, new TimeOnly(15, 0), 120));
        await app.Bookings.CancelAsync(cancelled.Id);

        var report = await app.Reports.GetSummaryAsync(Day, Day);

        Assert.Equal(2, report.TotalBookings);
        Assert.Equal(1, report.ConfirmedBookings);
        Assert.Equal(1, report.CancelledBookings);
        Assert.Equal(50m, report.CancellationRatePercent);
        Assert.Equal(4000m, report.TotalRevenue);
        Assert.Equal("Зал А", report.TopRoomName);
    }

    [Fact]
    public async Task RoomUtilization_ComputesShareOfWorkingHours()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var room = await CreateRoomAsync(app);

        await app.Bookings.CreateAsync(Booking(room.Id, new TimeOnly(10, 0), 240));

        var report = await app.Reports.GetRoomUtilizationAsync(Day, Day);

        var row = Assert.Single(report.Rooms);
        Assert.Equal(17m, report.WorkingHoursPerDay);       // 06:00–23:00
        Assert.Equal(4m, row.BookedHours);
        Assert.Equal(23.5m, row.UtilizationPercent);        // 4 / 17
    }

    [Fact]
    public async Task AmenityDemand_ReportsAttachRateAndRevenue()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var room = await CreateRoomAsync(app);
        var projectorId = room.Amenities.Single(a => a.Name == "Проєктор").AmenityId;

        await app.Bookings.CreateAsync(Booking(room.Id, new TimeOnly(10, 0), 120) with
        {
            AmenityIds = [projectorId]
        });

        await app.Bookings.CreateAsync(Booking(room.Id, new TimeOnly(15, 0), 120));

        var report = await app.Reports.GetAmenityDemandAsync(Day, Day);

        var row = Assert.Single(report.Amenities);
        Assert.Equal("Проєктор", row.Name);
        Assert.Equal(1, row.TimesOrdered);
        Assert.Equal(50m, row.AttachRatePercent);           // 1 із 2 бронювань
        Assert.Equal(500m, row.Revenue);
    }

    [Fact]
    public async Task PricingBands_AttributionSumsToRoomRevenue()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var room = await CreateRoomAsync(app);

        // 10:00–14:00: 4000 грн стандарту + 4600 грн піку.
        await app.Bookings.CreateAsync(Booking(room.Id, new TimeOnly(10, 0), 240));

        // 18:00–20:00: 3200 грн вечірньої смуги (замість 4000 грн за базовим тарифом).
        await app.Bookings.CreateAsync(Booking(room.Id, new TimeOnly(18, 0), 120));

        var report = await app.Reports.GetPricingBandPerformanceAsync(Day, Day);

        Assert.Equal(11_800m, report.TotalRoomRevenue);
        Assert.Equal(report.TotalRoomRevenue, report.Bands.Sum(b => b.Revenue));

        var evening = report.Bands.Single(b => b.Band == "Вечірні години");
        Assert.Equal(3200m, evening.Revenue);
        Assert.Equal(4000m, evening.RevenueAtBaseRate);
        Assert.Equal(-800m, evening.DiscountOrSurcharge);   // ціна вечірньої знижки

        var peak = report.Bands.Single(b => b.Band == "Пікові години");
        Assert.Equal(600m, peak.DiscountOrSurcharge);       // 4600 − 4000
    }

    [Fact]
    public async Task HourlyLoad_SplitsBookingAcrossHours()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var room = await CreateRoomAsync(app);

        await app.Bookings.CreateAsync(Booking(room.Id, new TimeOnly(10, 0), 120));

        var report = await app.Reports.GetHourlyLoadAsync(Day, Day);

        Assert.Equal(1m, report.Hours.Single(h => h.Hour == new TimeOnly(10, 0)).BookedHours);
        Assert.Equal(1m, report.Hours.Single(h => h.Hour == new TimeOnly(11, 0)).BookedHours);
        Assert.Equal(0m, report.Hours.Single(h => h.Hour == new TimeOnly(12, 0)).BookedHours);
        Assert.Equal(4000m, report.Hours.Sum(h => h.Revenue));
    }

    [Fact]
    public async Task Revenue_GroupsByDay()
    {
        await using var app = await TestApplication.CreateAsync(Now);
        var room = await CreateRoomAsync(app);

        await app.Bookings.CreateAsync(Booking(room.Id, new TimeOnly(10, 0), 120));
        await app.Bookings.CreateAsync(Booking(room.Id, new TimeOnly(10, 0), 120) with
        {
            Date = Day.AddDays(1)
        });

        var report = await app.Reports.GetRevenueAsync(Day, Day.AddDays(1), RevenueGranularity.Day);

        Assert.Equal(2, report.Buckets.Count);
        Assert.Equal(8000m, report.TotalRevenue);
        Assert.Equal("02.09.2024", report.Buckets[0].Label);
    }

    private static async Task<RoomResponse> CreateRoomAsync(TestApplication app)
    {
        var created = await app.Rooms.CreateAsync(new CreateRoomRequest(
            "Зал А", 50, 2000m, [new AmenityInput("Проєктор", 500m)]));

        return await app.Rooms.GetAsync(created.Id);
    }

    private static CreateBookingRequest Booking(Guid roomId, TimeOnly start, int durationMinutes) =>
        new(roomId, Day, start, durationMinutes, 10, "ТОВ «Приклад»", "office@example.com", null);
}
