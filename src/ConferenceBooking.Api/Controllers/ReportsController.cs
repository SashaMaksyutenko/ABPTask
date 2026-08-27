using ConferenceBooking.Api.Security;
using ConferenceBooking.Application.Common;
using ConferenceBooking.Application.Reports;
using ConferenceBooking.Application.Reports.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceBooking.Api.Controllers;

/// <summary>
/// Бізнес-звіти за бронюваннями.
///
/// Набір звітів підібрано під конкретні управлінські рішення: яку ціну ставити,
/// які зали тримати, які послуги закуповувати, чи працює тарифна сітка.
/// </summary>
[ApiController]
[Route("api/reports")]
[Produces("application/json")]
[Authorize(Policy = ApiPolicies.ViewReports)]
public sealed class ReportsController : ControllerBase
{
    /// <summary>Період за замовчуванням, якщо клієнт не вказав дати, — останні 30 днів.</summary>
    private const int DefaultPeriodDays = 30;

    private readonly IReportAppService _reports;
    private readonly IDateTimeProvider _clock;

    public ReportsController(IReportAppService reports, IDateTimeProvider clock)
    {
        _reports = reports;
        _clock = clock;
    }

    /// <summary>Зведені показники за період: виторг, кількість бронювань, середній чек, частка скасувань.</summary>
    /// <param name="from">Початок періоду (включно). За замовчуванням — 30 днів тому.</param>
    /// <param name="to">Кінець періоду (включно). За замовчуванням — сьогодні.</param>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(SummaryReport), StatusCodes.Status200OK)]
    public async Task<ActionResult<SummaryReport>> Summary(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var (start, end) = ResolvePeriod(from, to);
        return Ok(await _reports.GetSummaryAsync(start, end, cancellationToken));
    }

    /// <summary>
    /// Завантаженість залів: скільки годин продано з наявних, який виторг на годину
    /// і наскільки повно заповнюються зали.
    /// </summary>
    /// <remarks>
    /// Відповідає на питання, який зал недозавантажений і потребує знижки чи перепрофілювання,
    /// а який стабільно заповнений і витримає підвищення ціни. Стовпець середньої заповненості
    /// показує ще й те, чи не продають великий зал під маленькі зустрічі.
    /// </remarks>
    [HttpGet("room-utilization")]
    [ProducesResponseType(typeof(RoomUtilizationReport), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoomUtilizationReport>> RoomUtilization(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var (start, end) = ResolvePeriod(from, to);
        return Ok(await _reports.GetRoomUtilizationAsync(start, end, cancellationToken));
    }

    /// <summary>Динаміка виторгу з розбивкою за днями, тижнями або місяцями.</summary>
    /// <param name="granularity">Крок групування: Day, Week або Month.</param>
    [HttpGet("revenue")]
    [ProducesResponseType(typeof(RevenueReport), StatusCodes.Status200OK)]
    public async Task<ActionResult<RevenueReport>> Revenue(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] RevenueGranularity granularity = RevenueGranularity.Day,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = ResolvePeriod(from, to);
        return Ok(await _reports.GetRevenueAsync(start, end, granularity, cancellationToken));
    }

    /// <summary>
    /// Попит на послуги: як часто їх замовляють, у якій частці бронювань і скільки вони приносять.
    /// </summary>
    /// <remarks>
    /// Показує, які послуги варто докупити в решту залів, а які не окуповують обслуговування.
    /// </remarks>
    [HttpGet("amenity-demand")]
    [ProducesResponseType(typeof(AmenityDemandReport), StatusCodes.Status200OK)]
    public async Task<ActionResult<AmenityDemandReport>> AmenityDemand(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var (start, end) = ResolvePeriod(from, to);
        return Ok(await _reports.GetAmenityDemandAsync(start, end, cancellationToken));
    }

    /// <summary>Розподіл попиту за годинами доби.</summary>
    /// <remarks>
    /// Дає фактичну відповідь на питання, чи збігаються «пікові години» з тарифної сітки
    /// з реальним піком попиту, — і чи не варто зсунути межі смуг.
    /// </remarks>
    [HttpGet("hourly-load")]
    [ProducesResponseType(typeof(HourlyLoadReport), StatusCodes.Status200OK)]
    public async Task<ActionResult<HourlyLoadReport>> HourlyLoad(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var (start, end) = ResolvePeriod(from, to);
        return Ok(await _reports.GetHourlyLoadAsync(start, end, cancellationToken));
    }

    /// <summary>
    /// Ефективність тарифної сітки: скільки годин продано в кожній смузі та у скільки
    /// обійшлися знижки порівняно з базовим тарифом.
    /// </summary>
    /// <remarks>
    /// Показує ціну вечірньої та ранкової знижок у гривнях і те, скільки принесла пікова
    /// націнка, — щоб рішення про зміну тарифів спиралося на цифри, а не на відчуття.
    /// </remarks>
    [HttpGet("pricing-bands")]
    [ProducesResponseType(typeof(PricingBandReport), StatusCodes.Status200OK)]
    public async Task<ActionResult<PricingBandReport>> PricingBands(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var (start, end) = ResolvePeriod(from, to);
        return Ok(await _reports.GetPricingBandPerformanceAsync(start, end, cancellationToken));
    }

    /// <summary>Підставляє період за замовчуванням, якщо клієнт указав не всі межі.</summary>
    private (DateOnly From, DateOnly To) ResolvePeriod(DateOnly? from, DateOnly? to)
    {
        var end = to ?? _clock.Today;
        var start = from ?? end.AddDays(-DefaultPeriodDays);
        return (start, end);
    }
}
