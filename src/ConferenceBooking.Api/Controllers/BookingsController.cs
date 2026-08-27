using ConferenceBooking.Api.Security;
using ConferenceBooking.Application.Bookings;
using ConferenceBooking.Application.Bookings.Dtos;
using ConferenceBooking.Application.Rooms.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceBooking.Api.Controllers;

/// <summary>Пошук вільних залів, розрахунок вартості та бронювання.</summary>
[ApiController]
[Route("api/bookings")]
[Produces("application/json")]
[Authorize(Policy = ApiPolicies.BookRooms)]
public sealed class BookingsController : ControllerBase
{
    private readonly IBookingAppService _bookings;

    public BookingsController(IBookingAppService bookings) => _bookings = bookings;

    /// <summary>
    /// Шукає зали, вільні у вказаний проміжок і достатні за місткістю.
    /// </summary>
    /// <remarks>
    /// Приклад запиту:
    ///
    ///     GET /api/bookings/available-rooms?date=2024-09-01&amp;startTime=10:00&amp;endTime=14:00&amp;capacity=50
    ///
    /// Для кожного залу повертається орієнтовна вартість оренди саме на цей проміжок,
    /// уже з урахуванням тарифних смуг, — щоб зали можна було одразу порівняти за ціною.
    /// </remarks>
    /// <response code="200">Перелік вільних залів, відсортований за вартістю.</response>
    /// <response code="422">Проміжок виходить за межі робочих годин закладу.</response>
    [HttpGet("available-rooms")]
    [ProducesResponseType(typeof(IReadOnlyList<AvailableRoomResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyList<AvailableRoomResponse>>> FindAvailableRooms(
        [FromQuery] AvailabilitySearchRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _bookings.FindAvailableRoomsAsync(request, cancellationToken));

    /// <summary>
    /// Розраховує вартість оренди без створення бронювання.
    /// </summary>
    /// <remarks>
    /// Дає клієнту побачити ціну до підтвердження — і повертає розбивку за тарифними
    /// смугами, тож видно, звідки взялася сума.
    /// </remarks>
    /// <response code="200">Деталізований розрахунок вартості.</response>
    /// <response code="404">Залу з таким ідентифікатором немає.</response>
    [HttpPost("quote")]
    [ProducesResponseType(typeof(CostBreakdownResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CostBreakdownResponse>> Quote(
        [FromBody] QuoteRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _bookings.QuoteAsync(request, cancellationToken));

    /// <summary>
    /// Бронює зал.
    /// </summary>
    /// <remarks>
    /// Приклад запиту:
    ///
    ///     POST /api/bookings
    ///     {
    ///       "roomId": "1f0c...",
    ///       "date": "2024-09-01",
    ///       "startTime": "10:00",
    ///       "durationMinutes": 240,
    ///       "attendees": 45,
    ///       "customerName": "ТОВ «Приклад»",
    ///       "customerEmail": "office@example.com",
    ///       "amenityIds": ["8a31..."]
    ///     }
    ///
    /// У відповіді — підтвердження з повним розрахунком вартості.
    /// </remarks>
    /// <response code="201">Бронювання створено.</response>
    /// <response code="404">Залу з таким ідентифікатором немає.</response>
    /// <response code="409">Зал уже заброньовано на цей час.</response>
    /// <response code="422">Порушено бізнес-правило (місткість, робочі години, дата в минулому).</response>
    [HttpPost]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BookingResponse>> Create(
        [FromBody] CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        var booking = await _bookings.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = booking.Id }, booking);
    }

    /// <summary>Повертає бронювання за ідентифікатором.</summary>
    /// <response code="200">Бронювання знайдено.</response>
    /// <response code="404">Бронювання з таким ідентифікатором немає.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _bookings.GetAsync(id, cancellationToken));

    /// <summary>Скасовує бронювання і звільняє зал.</summary>
    /// <response code="200">Бронювання скасовано.</response>
    /// <response code="404">Бронювання з таким ідентифікатором немає.</response>
    /// <response code="409">Бронювання вже було скасоване.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(OperationResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OperationResultResponse>> Cancel(Guid id, CancellationToken cancellationToken) =>
        Ok(await _bookings.CancelAsync(id, cancellationToken));
}
