namespace ConferenceBooking.Api.Middleware;

/// <summary>
/// Додає заголовки безпеки до кожної відповіді.
/// Це API, а не сайт, але відповіді все одно можуть відкриватися в браузері
/// (наприклад, зі Swagger UI), тож базовий захист від MIME-sniffing і вбудовування у фрейм потрібен.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";

        // Сервер не має розповідати клієнтам, на чому він працює: це безкоштовна підказка
        // для добору відомих вразливостей.
        headers.Remove("Server");
        headers.Remove("X-Powered-By");

        return _next(context);
    }
}

/// <summary>Реєстрація middleware у конвеєрі.</summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();
}
