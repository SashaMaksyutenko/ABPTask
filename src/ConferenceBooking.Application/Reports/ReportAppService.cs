using System.Globalization;
using ConferenceBooking.Application.Common;
using ConferenceBooking.Application.Reports.Dtos;
using ConferenceBooking.Domain.Bookings;
using ConferenceBooking.Domain.Common;
using ConferenceBooking.Domain.Pricing;
using ConferenceBooking.Domain.Rooms;

namespace ConferenceBooking.Application.Reports;

/// <summary>Аналітика для бізнесу за бронюваннями.</summary>
public interface IReportAppService
{
    Task<SummaryReport> GetSummaryAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task<RoomUtilizationReport> GetRoomUtilizationAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<RevenueReport> GetRevenueAsync(
        DateOnly from,
        DateOnly to,
        RevenueGranularity granularity,
        CancellationToken cancellationToken = default);

    Task<AmenityDemandReport> GetAmenityDemandAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<HourlyLoadReport> GetHourlyLoadAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<PricingBandReport> GetPricingBandPerformanceAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IReportAppService"/>
public sealed class ReportAppService : IReportAppService
{
    /// <summary>Найдовший період, за який будується звіт, — захист від запитів, що кладуть базу.</summary>
    private const int MaxReportRangeDays = 731;

    private const int MoneyDecimals = 2;
    private const int PercentDecimals = 1;

    private readonly IBookingRepository _bookings;
    private readonly IConferenceRoomRepository _rooms;
    private readonly IRentalCostCalculator _calculator;
    private readonly PricingPolicy _pricingPolicy;

    public ReportAppService(
        IBookingRepository bookings,
        IConferenceRoomRepository rooms,
        IRentalCostCalculator calculator,
        PricingPolicy pricingPolicy)
    {
        _bookings = bookings;
        _rooms = rooms;
        _calculator = calculator;
        _pricingPolicy = pricingPolicy;
    }

    public async Task<SummaryReport> GetSummaryAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var period = ValidateRange(from, to);
        var all = await _bookings.ListByDateRangeAsync(from, to, cancellationToken).ConfigureAwait(false);
        var confirmed = all.Where(b => b.IsBlocking).ToArray();

        var cancelled = all.Count - confirmed.Length;
        var totalRevenue = confirmed.Sum(b => b.TotalCost);
        var bookedHours = confirmed.Sum(b => b.Hours);

        var topRoom = confirmed
            .GroupBy(b => b.RoomName)
            .OrderByDescending(g => g.Sum(b => b.TotalCost))
            .FirstOrDefault();

        var topAmenity = confirmed
            .SelectMany(b => b.Amenities)
            .GroupBy(a => a.Name)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        return new SummaryReport(
            period,
            all.Count,
            confirmed.Length,
            cancelled,
            Percent(cancelled, all.Count),
            Money(totalRevenue),
            Money(confirmed.Sum(b => b.RoomCost)),
            Money(confirmed.Sum(b => b.AmenitiesCost)),
            Money(Average(totalRevenue, confirmed.Length)),
            Round(Average(bookedHours, confirmed.Length), MoneyDecimals),
            Round(bookedHours, MoneyDecimals),
            confirmed.Select(b => b.CustomerEmail.ToLowerInvariant()).Distinct().Count(),
            topRoom?.Key,
            topAmenity?.Key);
    }

    public async Task<RoomUtilizationReport> GetRoomUtilizationAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var period = ValidateRange(from, to);

        var rooms = await _rooms.ListAsync(cancellationToken).ConfigureAwait(false);
        var bookings = await _bookings.ListByDateRangeAsync(from, to, cancellationToken).ConfigureAwait(false);

        var days = to.DayNumber - from.DayNumber + 1;
        var workingHoursPerDay = (decimal)(_pricingPolicy.ClosingTime - _pricingPolicy.OpeningTime).TotalHours;
        var availableHours = workingHoursPerDay * days;

        var byRoom = bookings
            .Where(b => b.IsBlocking)
            .GroupBy(b => b.RoomId)
            .ToDictionary(g => g.Key, g => g.ToArray());

        var rows = rooms
            .Select(room =>
            {
                var roomBookings = byRoom.TryGetValue(room.Id, out var found) ? found : [];
                var hours = roomBookings.Sum(b => b.Hours);
                var revenue = roomBookings.Sum(b => b.TotalCost);
                var averageAttendees = Average(roomBookings.Sum(b => (decimal)b.Attendees), roomBookings.Length);

                return new RoomUtilizationRow(
                    room.Id,
                    room.Name,
                    room.Capacity,
                    roomBookings.Length,
                    Round(hours, MoneyDecimals),
                    Round(availableHours, MoneyDecimals),
                    Percent(hours, availableHours),
                    Money(revenue),
                    Money(Average(revenue, hours)),
                    Round(averageAttendees, PercentDecimals),
                    Percent(averageAttendees, room.Capacity));
            })
            .OrderByDescending(row => row.UtilizationPercent)
            .ToArray();

        return new RoomUtilizationReport(period, Round(workingHoursPerDay, MoneyDecimals), rows);
    }

    public async Task<RevenueReport> GetRevenueAsync(
        DateOnly from,
        DateOnly to,
        RevenueGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        var period = ValidateRange(from, to);
        var bookings = await _bookings.ListByDateRangeAsync(from, to, cancellationToken).ConfigureAwait(false);
        var confirmed = bookings.Where(b => b.IsBlocking).ToArray();

        var buckets = confirmed
            .GroupBy(b => BucketStart(b.Date, granularity))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var bucketEnd = BucketEnd(g.Key, granularity);
                return new RevenueBucket(
                    FormatBucket(g.Key, granularity),
                    g.Key,
                    bucketEnd,
                    g.Count(),
                    Money(g.Sum(b => b.RoomCost)),
                    Money(g.Sum(b => b.AmenitiesCost)),
                    Money(g.Sum(b => b.TotalCost)));
            })
            .ToArray();

        return new RevenueReport(period, granularity, Money(confirmed.Sum(b => b.TotalCost)), buckets);
    }

    public async Task<AmenityDemandReport> GetAmenityDemandAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var period = ValidateRange(from, to);
        var bookings = await _bookings.ListByDateRangeAsync(from, to, cancellationToken).ConfigureAwait(false);
        var confirmed = bookings.Where(b => b.IsBlocking).ToArray();

        var rows = confirmed
            .SelectMany(b => b.Amenities)
            .GroupBy(a => new { a.AmenityId, a.Name })
            .Select(g =>
            {
                var revenue = g.Sum(a => a.Price);
                return new AmenityDemandRow(
                    g.Key.AmenityId,
                    g.Key.Name,
                    g.Count(),
                    Percent(g.Count(), confirmed.Length),
                    Money(revenue),
                    Money(Average(revenue, g.Count())));
            })
            .OrderByDescending(row => row.Revenue)
            .ToArray();

        return new AmenityDemandReport(period, confirmed.Length, rows);
    }

    public async Task<HourlyLoadReport> GetHourlyLoadAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var period = ValidateRange(from, to);
        var bookings = await _bookings.ListByDateRangeAsync(from, to, cancellationToken).ConfigureAwait(false);
        var confirmed = bookings.Where(b => b.IsBlocking).ToArray();

        var openingHour = _pricingPolicy.OpeningTime.Hour;
        var closingHour = _pricingPolicy.ClosingTime.Hour;

        var rows = new List<HourlyLoadRow>(Math.Max(0, closingHour - openingHour));

        for (var hour = openingHour; hour < closingHour; hour++)
        {
            var slotStart = new TimeOnly(hour, 0);
            var slotEnd = slotStart.AddHours(1);

            var touching = confirmed
                .Where(b => OverlapHours(b, slotStart, slotEnd) > 0m)
                .ToArray();

            var hoursInSlot = touching.Sum(b => OverlapHours(b, slotStart, slotEnd));

            // Виторг години — пропорційна частка вартості бронювання, що припадає на цю годину.
            var revenue = touching.Sum(b => b.Hours == 0m
                ? 0m
                : b.TotalCost * OverlapHours(b, slotStart, slotEnd) / b.Hours);

            rows.Add(new HourlyLoadRow(
                slotStart,
                touching.Length,
                Round(hoursInSlot, MoneyDecimals),
                Money(revenue)));
        }

        return new HourlyLoadReport(period, rows);
    }

    public async Task<PricingBandReport> GetPricingBandPerformanceAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var period = ValidateRange(from, to);
        var bookings = await _bookings.ListByDateRangeAsync(from, to, cancellationToken).ConfigureAwait(false);
        var confirmed = bookings.Where(b => b.IsBlocking).ToArray();

        var accumulator = new Dictionary<string, BandAccumulator>();

        foreach (var booking in confirmed)
        {
            foreach (var segment in AttributeToBands(booking))
            {
                if (!accumulator.TryGetValue(segment.BandName, out var band))
                {
                    band = new BandAccumulator(segment.Multiplier);
                    accumulator[segment.BandName] = band;
                }

                band.Add(segment.Hours, segment.Amount);
            }
        }

        var totalHours = accumulator.Values.Sum(v => v.Hours);
        var totalRevenue = accumulator.Values.Sum(v => v.Revenue);
        var totalAtBase = accumulator.Values.Sum(v => v.RevenueAtBaseRate);

        var rows = accumulator
            .Select(entry => new PricingBandRow(
                entry.Key,
                entry.Value.Multiplier,
                Round(entry.Value.Hours, MoneyDecimals),
                Percent(entry.Value.Hours, totalHours),
                Money(entry.Value.Revenue),
                Money(entry.Value.RevenueAtBaseRate),
                Money(entry.Value.Revenue - entry.Value.RevenueAtBaseRate)))
            .OrderByDescending(row => row.BookedHours)
            .ToArray();

        return new PricingBandReport(period, Money(totalRevenue), Money(totalRevenue - totalAtBase), rows);
    }

    /// <summary>
    /// Розкладає вартість оренди бронювання за тарифними смугами.
    /// Базова ставка виводиться із зафіксованої суми, а не читається із залу: ціна залу могла
    /// змінитися після бронювання, і тоді звіт розійшовся б із фактичним виторгом.
    /// </summary>
    private IReadOnlyList<RentalCostSegment> AttributeToBands(Booking booking)
    {
        var reference = _calculator.Calculate(1m, booking.Period, []);
        if (reference.RoomCost == 0m)
        {
            return [];
        }

        var effectiveBaseRate = booking.RoomCost / reference.RoomCost;
        return _calculator.Calculate(effectiveBaseRate, booking.Period, []).Segments;
    }

    /// <summary>Скільки годин бронювання припадає на вказану годину доби.</summary>
    private static decimal OverlapHours(Booking booking, TimeOnly slotStart, TimeOnly slotEnd)
    {
        var start = TimeOnly.FromDateTime(booking.StartAt);
        var end = TimeOnly.FromDateTime(booking.EndAt);

        var overlapStart = start > slotStart ? start : slotStart;
        var overlapEnd = end < slotEnd ? end : slotEnd;

        return overlapEnd <= overlapStart ? 0m : (decimal)(overlapEnd - overlapStart).TotalHours;
    }

    private static ReportPeriod ValidateRange(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            throw new DomainException("invalid_report_range", "Дата «до» не може бути ранішою за дату «від».");
        }

        if (to.DayNumber - from.DayNumber + 1 > MaxReportRangeDays)
        {
            throw new DomainException(
                "report_range_too_wide",
                $"Максимальний період звіту — {MaxReportRangeDays} днів.");
        }

        return new ReportPeriod(from, to);
    }

    private static DateOnly BucketStart(DateOnly date, RevenueGranularity granularity) => granularity switch
    {
        RevenueGranularity.Day => date,
        RevenueGranularity.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)), // тиждень із понеділка
        RevenueGranularity.Month => new DateOnly(date.Year, date.Month, 1),
        _ => date
    };

    private static DateOnly BucketEnd(DateOnly start, RevenueGranularity granularity) => granularity switch
    {
        RevenueGranularity.Day => start,
        RevenueGranularity.Week => start.AddDays(6),
        RevenueGranularity.Month => start.AddMonths(1).AddDays(-1),
        _ => start
    };

    private static string FormatBucket(DateOnly start, RevenueGranularity granularity) => granularity switch
    {
        RevenueGranularity.Day => start.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
        RevenueGranularity.Week => $"{start:dd.MM.yyyy}–{start.AddDays(6):dd.MM.yyyy}",
        RevenueGranularity.Month => start.ToString("MM.yyyy", CultureInfo.InvariantCulture),
        _ => start.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
    };

    private static decimal Average(decimal total, decimal count) => count == 0m ? 0m : total / count;

    private static decimal Percent(decimal part, decimal whole) =>
        whole == 0m ? 0m : Round(part / whole * 100m, PercentDecimals);

    private static decimal Money(decimal value) => Round(value, MoneyDecimals);

    private static decimal Round(decimal value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    /// <summary>Накопичувач показників однієї тарифної смуги під час обходу бронювань.</summary>
    private sealed class BandAccumulator
    {
        public BandAccumulator(decimal multiplier) => Multiplier = multiplier;

        public decimal Multiplier { get; }

        public decimal Hours { get; private set; }

        public decimal Revenue { get; private set; }

        /// <summary>Скільки той самий час коштував би за базовим тарифом, без знижок і націнок.</summary>
        public decimal RevenueAtBaseRate { get; private set; }

        public void Add(decimal hours, decimal amount)
        {
            Hours += hours;
            Revenue += amount;
            RevenueAtBaseRate += Multiplier == 0m ? 0m : amount / Multiplier;
        }
    }
}
