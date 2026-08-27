using System.Reflection;
using ConferenceBooking.Api.Security;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace ConferenceBooking.Api.Swagger;

/// <summary>Налаштування документації OpenAPI/Swagger.</summary>
public static class SwaggerConfiguration
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Conference Booking API",
                Version = "v1",
                Description =
                    "API для управління конференц-залами, бронюваннями та розрахунку вартості оренди.\n\n" +
                    "**Автентифікація.** Усі методи, крім `/health`, вимагають API-ключ у заголовку `X-Api-Key`. " +
                    "Натисніть **Authorize** і вкажіть ключ.\n\n" +
                    "**Ролі.** `Administrator` — керування залами та звіти; `Client` — пошук і бронювання."
            });

            // XML-коментарі з коду стають описами в Swagger: документація живе поруч
            // із кодом і не встигає застаріти окремо від нього.
            IncludeXmlComments(path => options.IncludeXmlComments(path));

            var scheme = new OpenApiSecurityScheme
            {
                Name = "X-Api-Key",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "API-ключ доступу. Тестові ключі — у README проєкту.",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = ApiKeyAuthenticationHandler.SchemeName
                }
            };

            options.AddSecurityDefinition(ApiKeyAuthenticationHandler.SchemeName, scheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });

            // TimeOnly за замовчуванням розкладається на години/хвилини/тіки —
            // у документації це виглядало б як складний об'єкт замість "10:00".
            options.MapType<TimeOnly>(() => new OpenApiSchema
            {
                Type = "string",
                Format = "time",
                Example = new OpenApiString("10:00:00")
            });

            options.MapType<TimeOnly?>(() => new OpenApiSchema
            {
                Type = "string",
                Format = "time",
                Nullable = true,
                Example = new OpenApiString("10:00:00")
            });

            options.MapType<DateOnly>(() => new OpenApiSchema
            {
                Type = "string",
                Format = "date",
                Example = new OpenApiString("2024-09-01")
            });

            options.MapType<DateOnly?>(() => new OpenApiSchema
            {
                Type = "string",
                Format = "date",
                Nullable = true,
                Example = new OpenApiString("2024-09-01")
            });

            options.SupportNonNullableReferenceTypes();
        });

        return services;
    }

    public static WebApplication UseSwaggerDocumentation(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Conference Booking API v1");
            options.DocumentTitle = "Conference Booking API";
            options.DisplayRequestDuration();
        });

        return app;
    }

    /// <summary>
    /// Підключає XML-документацію API та прикладного шару, якщо файли згенеровано.
    /// Відсутній файл не має валити застосунок: документація — не критична для роботи функція.
    /// </summary>
    private static void IncludeXmlComments(Action<string> include)
    {
        var baseDirectory = AppContext.BaseDirectory;

        foreach (var assembly in new[]
                 {
                     Assembly.GetExecutingAssembly(),
                     typeof(Application.DependencyInjection).Assembly
                 })
        {
            var xmlPath = Path.Combine(baseDirectory, $"{assembly.GetName().Name}.xml");
            if (File.Exists(xmlPath))
            {
                include(xmlPath);
            }
        }
    }
}
