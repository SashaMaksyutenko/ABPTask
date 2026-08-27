using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ConferenceBooking.Api.Security;

/// <summary>
/// Автентифікація за API-ключем у заголовку запиту.
///
/// Ключі обрано як механізм доступу для сервіс-ту-сервіс інтеграцій: клієнти цього API —
/// системи партнерів, а не люди в браузері. Для користувацьких сценаріїв поверх цього
/// шару природно додається OAuth2/OIDC, не змінюючи контролерів: вони спираються на політики,
/// а не на конкретну схему автентифікації.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";

    private readonly ApiKeyOptions _apiKeyOptions;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<ApiKeyOptions> apiKeyOptions)
        : base(options, logger, encoder) => _apiKeyOptions = apiKeyOptions.Value;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(_apiKeyOptions.HeaderName, out var provided))
        {
            // NoResult, а не Fail: відсутність ключа — це «не автентифікований», і далі
            // рішення ухвалює авторизація. Публічні маршрути мають лишатися доступними.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var presentedKey = provided.ToString();
        if (string.IsNullOrWhiteSpace(presentedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Порожній API-ключ."));
        }

        var matched = _apiKeyOptions.Keys.FirstOrDefault(entry => IsMatch(entry.Key, presentedKey));
        if (matched is null)
        {
            Logger.LogWarning(
                "Відхилено запит із невідомим API-ключем. IP: {RemoteIp}",
                Context.Connection.RemoteIpAddress);

            return Task.FromResult(AuthenticateResult.Fail("Невідомий API-ключ."));
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, matched.Owner),
                new Claim(ClaimTypes.Role, matched.Role)
            ],
            SchemeName);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// Порівняння ключів за сталий час. Звичайне порівняння рядків завершується на першому
    /// відмінному символі, і за різницею часу відповіді ключ можна відновити символ за символом.
    /// </summary>
    private static bool IsMatch(string configuredKey, string presentedKey)
    {
        if (string.IsNullOrEmpty(configuredKey))
        {
            return false;
        }

        // Хешування зрівнює довжини — інакше сам факт збігу довжини теж витікає через час відповіді.
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
        var presentedHash = SHA256.HashData(Encoding.UTF8.GetBytes(presentedKey));

        return CryptographicOperations.FixedTimeEquals(configuredHash, presentedHash);
    }
}
