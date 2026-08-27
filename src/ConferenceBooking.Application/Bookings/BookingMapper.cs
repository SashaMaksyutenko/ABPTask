using ConferenceBooking.Application.Bookings.Dtos;
using ConferenceBooking.Domain.Bookings;
using ConferenceBooking.Domain.Pricing;

namespace ConferenceBooking.Application.Bookings;

/// <summary>Перетворення бронювань і розрахунків вартості на контракти API.</summary>
public static class BookingMapper
{
    public static CostBreakdownResponse ToResponse(this RentalCostBreakdown breakdown) =>
        new(
            breakdown.RoomCost,
            breakdown.AmenitiesCost,
            breakdown.Total,
            breakdown.Segments
                .Select(s => new CostSegmentResponse(s.BandName, s.From, s.To, s.Hours, s.Multiplier, s.Amount))
                .ToArray(),
            breakdown.Amenities
                .Select(a => new ChargedAmenityResponse(a.AmenityId, a.Name, a.Price))
                .ToArray());

    /// <summary>
    /// Перетворює збережене бронювання на відповідь API.
    /// Ділянки тарифів передаються окремо: у бронюванні зберігаються підсумкові суми,
    /// а деталізацію відновлює калькулятор за тією ж тарифною політикою.
    /// </summary>
    public static BookingResponse ToResponse(this Booking booking, IReadOnlyList<CostSegmentResponse> segments)
    {
        var amenities = booking.Amenities
            .Select(a => new ChargedAmenityResponse(a.AmenityId, a.Name, a.Price))
            .ToArray();

        var cost = new CostBreakdownResponse(
            booking.RoomCost,
            booking.AmenitiesCost,
            booking.TotalCost,
            segments,
            amenities);

        return new BookingResponse(
            booking.Id,
            booking.RoomId,
            booking.RoomName,
            booking.Date,
            TimeOnly.FromDateTime(booking.StartAt),
            TimeOnly.FromDateTime(booking.EndAt),
            booking.Hours,
            booking.Attendees,
            booking.CustomerName,
            booking.CustomerEmail,
            booking.Status.ToString(),
            cost,
            booking.CreatedAtUtc);
    }
}
