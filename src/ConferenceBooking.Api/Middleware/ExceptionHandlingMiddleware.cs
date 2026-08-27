using ConferenceBooking.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceBooking.Api.Middleware;

/// <summary>
/// Єдина точка перетворення винятків на відповіді API.
///
/// Без неї кожен контролер обростає try/catch, а частина винятків рано чи пізно
/// витікає назовні у вигляді 500 зі стектрейсом. Тут же вирішується й питання безпеки:
/// клієнт отримує тільки те, що йому належить знати.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private const string ProblemContentType = "application/problem+json";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception).ConfigureAwait(false);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            // Відповідь уже пішла клієнту — переписати статус неможливо, лишається лише зафіксувати збій.
            _logger.LogError(exception, "Виняток після початку відповіді; тіло змінити не можна.");
            throw exception;
        }

        var problem = Translate(context, exception);

        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = ProblemContentType;

        await context.Response.WriteAsJsonAsync(problem, problem.GetType()).ConfigureAwait(false);
    }

    private ProblemDetails Translate(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;

        switch (exception)
        {
            case ValidationException validation:
                _logger.LogInformation("Некоректний запит: {Errors}", validation.Message);
                return BuildValidationProblem(validation, traceId);

            case EntityNotFoundException notFound:
                _logger.LogInformation("Ресурс не знайдено: {Message}", notFound.Message);
                return Build(StatusCodes.Status404NotFound, "Ресурс не знайдено", notFound.Message, notFound.Code, traceId);

            case ConflictException conflict:
                _logger.LogInformation("Конфлікт стану: {Message}", conflict.Message);
                return Build(StatusCodes.Status409Conflict, "Конфлікт", conflict.Message, conflict.Code, traceId);

            case DomainException domain:
                _logger.LogInformation("Порушено бізнес-правило: {Message}", domain.Message);
                return Build(
                    StatusCodes.Status422UnprocessableEntity,
                    "Неможливо виконати операцію",
                    domain.Message,
                    domain.Code,
                    traceId);

            case OperationCanceledException when context.RequestAborted.IsCancellationRequested:
                _logger.LogInformation("Запит скасовано клієнтом.");
                return Build(StatusCodes.Status499ClientClosedRequest, "Запит скасовано", "Клієнт розірвав з'єднання.", "request_cancelled", traceId);

            default:
                // Деталі невідомої помилки лишаються в логах: повідомлення виняткy може містити
                // рядок підключення, шлях на диску чи фрагмент запиту.
                _logger.LogError(exception, "Необроблений виняток. TraceId: {TraceId}", traceId);

                var detail = _environment.IsDevelopment()
                    ? exception.ToString()
                    : "Сталася внутрішня помилка. Зверніться до підтримки та вкажіть traceId.";

                return Build(
                    StatusCodes.Status500InternalServerError,
                    "Внутрішня помилка сервера",
                    detail,
                    "internal_error",
                    traceId);
        }
    }

    private static ValidationProblemDetails BuildValidationProblem(ValidationException exception, string traceId)
    {
        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                group => string.IsNullOrEmpty(group.Key) ? "request" : group.Key,
                group => group.Select(e => e.ErrorMessage).Distinct().ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Некоректні вхідні дані",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
        };

        problem.Extensions["code"] = "validation_failed";
        problem.Extensions["traceId"] = traceId;

        return problem;
    }

    private static ProblemDetails Build(int status, string title, string detail, string code, string traceId)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };

        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = traceId;

        return problem;
    }
}

/// <summary>Реєстрація middleware у конвеєрі.</summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}
