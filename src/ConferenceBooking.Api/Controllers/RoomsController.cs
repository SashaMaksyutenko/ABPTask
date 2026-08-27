using ConferenceBooking.Api.Security;
using ConferenceBooking.Application.Rooms;
using ConferenceBooking.Application.Rooms.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceBooking.Api.Controllers;

/// <summary>Керування конференц-залами та їхніми послугами.</summary>
[ApiController]
[Route("api/rooms")]
[Produces("application/json")]
public sealed class RoomsController : ControllerBase
{
    private readonly IRoomAppService _rooms;

    public RoomsController(IRoomAppService rooms) => _rooms = rooms;

    /// <summary>Повертає перелік усіх активних залів із їхніми послугами.</summary>
    /// <response code="200">Перелік залів.</response>
    [HttpGet]
    [Authorize(Policy = ApiPolicies.BookRooms)]
    [ProducesResponseType(typeof(IReadOnlyList<RoomResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoomResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await _rooms.ListAsync(cancellationToken));

    /// <summary>Повертає зал за ідентифікатором.</summary>
    /// <param name="id">Ідентифікатор залу.</param>
    /// <response code="200">Зал знайдено.</response>
    /// <response code="404">Залу з таким ідентифікатором немає.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = ApiPolicies.BookRooms)]
    [ProducesResponseType(typeof(RoomResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _rooms.GetAsync(id, cancellationToken));

    /// <summary>
    /// Додає конференц-зал.
    /// </summary>
    /// <remarks>
    /// Приклад запиту:
    ///
    ///     POST /api/rooms
    ///     {
    ///       "name": "Зал А",
    ///       "capacity": 50,
    ///       "basePricePerHour": 2000,
    ///       "amenities": [
    ///         { "name": "Проєктор", "price": 500 },
    ///         { "name": "Wi-Fi", "price": 300 }
    ///       ]
    ///     }
    /// </remarks>
    /// <response code="201">Зал створено; повертається його унікальний ID.</response>
    /// <response code="400">Вхідні дані не пройшли валідацію.</response>
    /// <response code="409">Зал із такою назвою вже існує.</response>
    [HttpPost]
    [Authorize(Policy = ApiPolicies.ManageRooms)]
    [ProducesResponseType(typeof(RoomCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoomCreatedResponse>> Create(
        [FromBody] CreateRoomRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _rooms.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>
    /// Редагує інформацію про зал. Передаються лише ті поля, які змінюються.
    /// </summary>
    /// <remarks>
    /// Якщо передати масив <c>amenities</c>, перелік послуг залу буде замінено повністю.
    /// Щоб додати одну послугу, не чіпаючи інші, використовуйте
    /// <c>POST /api/rooms/{id}/amenities</c>.
    /// </remarks>
    /// <response code="200">Зал оновлено.</response>
    /// <response code="404">Залу з таким ідентифікатором немає.</response>
    /// <response code="409">Назва вже зайнята іншим залом.</response>
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = ApiPolicies.ManageRooms)]
    [ProducesResponseType(typeof(RoomResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoomResponse>> Update(
        Guid id,
        [FromBody] UpdateRoomRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _rooms.UpdateAsync(id, request, cancellationToken));

    /// <summary>Видаляє конференц-зал.</summary>
    /// <remarks>
    /// Застосовується м'яке видалення: зал зникає з пошуку та переліків, але історія
    /// бронювань і фінансова звітність за минулі періоди лишаються коректними.
    /// Зал з активними майбутніми бронюваннями видалити не можна.
    /// </remarks>
    /// <response code="200">Зал видалено.</response>
    /// <response code="404">Залу з таким ідентифікатором немає.</response>
    /// <response code="409">У залу є активні бронювання на майбутні дати.</response>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ApiPolicies.ManageRooms)]
    [ProducesResponseType(typeof(OperationResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OperationResultResponse>> Delete(Guid id, CancellationToken cancellationToken) =>
        Ok(await _rooms.DeleteAsync(id, cancellationToken));

    /// <summary>Додає послугу до залу або оновлює її ціну, якщо така послуга вже є.</summary>
    /// <response code="200">Перелік послуг залу оновлено.</response>
    /// <response code="404">Залу з таким ідентифікатором немає.</response>
    [HttpPost("{id:guid}/amenities")]
    [Authorize(Policy = ApiPolicies.ManageRooms)]
    [ProducesResponseType(typeof(RoomResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomResponse>> AddAmenity(
        Guid id,
        [FromBody] AmenityInput amenity,
        CancellationToken cancellationToken) =>
        Ok(await _rooms.AddAmenityAsync(id, amenity, cancellationToken));

    /// <summary>Прибирає послугу із залу.</summary>
    /// <response code="200">Послугу прибрано.</response>
    /// <response code="404">Залу або послуги в цьому залі немає.</response>
    [HttpDelete("{id:guid}/amenities/{amenityId:guid}")]
    [Authorize(Policy = ApiPolicies.ManageRooms)]
    [ProducesResponseType(typeof(RoomResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomResponse>> RemoveAmenity(
        Guid id,
        Guid amenityId,
        CancellationToken cancellationToken) =>
        Ok(await _rooms.RemoveAmenityAsync(id, amenityId, cancellationToken));
}
