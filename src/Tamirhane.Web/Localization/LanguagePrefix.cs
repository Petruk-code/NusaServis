using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace Tamirhane.Web.Localization;

/// <summary>
/// Bahasa di URL (SEO): Indonesia tanpa prefix (/privacy), Inggris di /en (/en/privacy).
/// "/en" dipindah ke PathBase agar routing/link/redirect ASP.NET tetap normal.
/// </summary>
public static class LanguagePrefix
{
    public const string English = "/en";

    /// <summary>Prefix URL untuk budaya: Inggris "/en", default kosong.</summary>
    public static string For(CultureInfo culture) => culture.TwoLetterISOLanguageName == "en" ? English : "";

    public static bool IsEnglish(HttpContext context) =>
        context.Request.PathBase.Value?.EndsWith(English, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// 1) Request "/en/..." memindahkan prefix ke PathBase.
    /// 2) Request tanpa prefix diarahkan ke /en hanya jika cookie bahasa memilih English.
    /// </summary>
    public static IApplicationBuilder UseLanguagePrefix(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var request = context.Request;
            if (request.Path.StartsWithSegments(English, StringComparison.OrdinalIgnoreCase, out var rest))
            {
                request.PathBase = request.PathBase.Add(English);
                request.Path = rest.HasValue ? rest : "/";
            }
            else if (PrefersEnglish(context) && IsPage(request))
            {
                context.Response.Redirect(request.PathBase + English + request.Path + request.QueryString);
                return;
            }
            await next();
        });

    /// <summary>Budaya dari URL: /en English, selain itu Indonesia (default).</summary>
    public sealed class Provider : RequestCultureProvider
    {
        public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext) =>
            Task.FromResult<ProviderCultureResult?>(new((IsEnglish(httpContext) ? AppCultures.English : AppCultures.Indonesian).Name));
    }

    private static bool PrefersEnglish(HttpContext context) =>
        context.Request.Cookies.TryGetValue(AppCultures.CookieName, out var cookie)
        && CookieRequestCultureProvider.ParseCookieValue(cookie)?.UICultures.FirstOrDefault() is { } chosen
        && AppCultures.From(chosen.Value)?.TwoLetterISOLanguageName == "en";

    private static bool IsPage(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            return false;
        var path = request.Path.Value ?? "/";
        if (Path.HasExtension(path))
            return false;
        string[] skipped = ["/t", "/health", "/healthz", "/language", "/error"];
        return !skipped.Any(s => request.Path.StartsWithSegments(s, StringComparison.OrdinalIgnoreCase));
    }
}
