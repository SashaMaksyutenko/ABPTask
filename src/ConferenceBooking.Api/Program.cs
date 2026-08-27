using System.Globalization;
using System.Threading.RateLimiting;
using ConferenceBooking.Api.Filters;
using ConferenceBooking.Api.Middleware;
using ConferenceBooking.Api.Security;
using ConferenceBooking.Api.Swagger;
using ConferenceBooking.Application;
using ConferenceBooking.Application.Configuration;
using ConferenceBooking.Infrastructure;
using ConferenceBooking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Формати дат і чисел у відповідях не мають залежати від локалі сервера:
// інакше та сама сума приїде клієнту то з крапкою, то з комою.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

// ── Конфігурація ─────────────────────────────────────────────────────────────
var pricingOptions = builder.Configuration
    .GetSection(PricingOptions.SectionName)
    .Get<PricingOptions>() ?? new PricingOptions();

var bookingPolicyOptions = builder.Configuration
    .GetSection(BookingPolicyOptions.SectionName)
    .Get<BookingPolicyOptions>() ?? new BookingPolicyOptions();

builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection(ApiKeyOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=conference-booking.db";

// ── Шари застосунку ──────────────────────────────────────────────────────────
builder.Services.AddInfrastructure(
    connectionString,
    builder.Configuration["Venue:TimeZoneId"]);

builder.Services.AddApplication(pricingOptions, bookingPolicyOptions);

// ── Веб-шар ──────────────────────────────────────────────────────────────────
builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .ConfigureApiBehaviorOptions(options =>
    {
        // Помилки прив'язки моделі мають повертатися в тому ж форматі, що й усі інші
        // помилки застосунку, тож віддаємо їх через спільний обробник.
        options.SuppressModelStateInvalidFilter = false;
    });

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerDocumentation();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ConferenceBookingDbContext>("database");

// ── Безпека ──────────────────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName,
        _ => { });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(ApiPolicies.ManageRooms, policy => policy.RequireRole(ApiRoles.Administrator))
    .AddPolicy(ApiPolicies.ViewReports, policy => policy.RequireRole(ApiRoles.Administrator))
    .AddPolicy(ApiPolicies.BookRooms, policy =>
        policy.RequireRole(ApiRoles.Administrator, ApiRoles.Client))
    // Маршрути без явної політики все одно вимагають автентифікації:
    // забути [Authorize] на новому контролері не має означати відкритий доступ.
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// Обмеження частоти запитів: захист від перебору ключів і від того, щоб один клієнт
// вичерпав ресурси сервісу для всіх інших.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var partitionKey = context.Request.Headers["X-Api-Key"].FirstOrDefault()
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

    if (allowedOrigins.Length == 0)
    {
        // Порожній список означає «браузерні клієнти не передбачені».
        // Ставити AllowAnyOrigin за замовчуванням не можна: це тихо відкриє API всім сайтам.
        policy.WithOrigins().AllowAnyHeader().AllowAnyMethod();
        return;
    }

    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();

// ── Конвеєр обробки запитів ──────────────────────────────────────────────────
app.UseExceptionHandling();
app.UseSecurityHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseSwaggerDocumentation();

app.UseRateLimiter();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

// ── Підготовка бази ──────────────────────────────────────────────────────────
await using (var scope = app.Services.CreateAsyncScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

await app.RunAsync();

/// <summary>
/// Точка входу оголошена частковим класом, щоб інтеграційні тести могли підняти
/// застосунок через WebApplicationFactory.
/// </summary>
public partial class Program
{
    protected Program()
    {
    }
}
