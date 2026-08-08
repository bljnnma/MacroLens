namespace Scorecard.Api.Infrastructure.Localization;

/// <summary>
/// The API resolves locale once and returns already-localized single strings.
/// Clients never receive {mn, en} pairs: otherwise every component would carry
/// branching logic and payloads would double for no benefit.
/// </summary>
public interface ILocaleContext
{
    string Locale { get; }
}

public sealed class LocaleContext : ILocaleContext
{
    public const string DefaultLocale = "mn";
    public const string CookieName = "NEXT_LOCALE";
    public static readonly string[] Supported = ["mn", "en"];

    public string Locale { get; internal set; } = DefaultLocale;
}

/// <summary>
/// Resolution order: user preference (future, via JWT claim) -> cookie ->
/// Accept-Language -> default. The cookie name matches the one next-intl already
/// writes, so the frontend and backend share a single source of truth.
/// </summary>
public sealed class LocaleMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ILocaleContext locale)
    {
        if (locale is LocaleContext resolvable)
            resolvable.Locale = Resolve(context);

        // Any cache in front of this must vary per locale, or one user's language
        // leaks to the next.
        context.Response.Headers.Append("Vary", "Accept-Language, Cookie");

        await next(context);
    }

    private static string Resolve(HttpContext context)
    {
        var query = context.Request.Query["lang"].FirstOrDefault();
        if (IsSupported(query)) return query!.ToLowerInvariant();

        if (context.Request.Cookies.TryGetValue(LocaleContext.CookieName, out var cookie) && IsSupported(cookie))
            return cookie!.ToLowerInvariant();

        var header = context.Request.Headers.AcceptLanguage.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(header))
        {
            foreach (var part in header.Split(','))
            {
                var tag = part.Split(';')[0].Trim();
                var primary = tag.Split('-')[0];
                if (IsSupported(primary)) return primary.ToLowerInvariant();
            }
        }

        return LocaleContext.DefaultLocale;
    }

    // Strict allowlist: an unbounded locale value would reach straight into
    // dictionary lookups downstream.
    private static bool IsSupported(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        LocaleContext.Supported.Contains(value.ToLowerInvariant());
}
