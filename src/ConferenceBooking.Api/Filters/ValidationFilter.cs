using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ConferenceBooking.Api.Filters;

/// <summary>
/// Проганяє аргументи дії через зареєстровані валідатори FluentValidation.
///
/// Виконується як фільтр, а не викликається вручну в кожному контролері: інакше
/// перевірку рано чи пізно забувають додати в новому методі, і невалідні дані
/// доходять до доменного шару.
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _services;

    public ValidationFilter(IServiceProvider services) => _services = services;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator
                .ValidateAsync(validationContext, context.HttpContext.RequestAborted)
                .ConfigureAwait(false);

            if (!result.IsValid)
            {
                // Кидаємо виняток, а не формуємо відповідь тут: перетворення помилок
                // на HTTP лежить на middleware, і формат відповіді має бути один на весь застосунок.
                throw new ValidationException(result.Errors);
            }
        }

        await next().ConfigureAwait(false);
    }
}
