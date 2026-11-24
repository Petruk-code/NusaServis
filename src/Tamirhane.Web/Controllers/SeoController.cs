using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tamirhane.Web.Localization;

namespace Tamirhane.Web.Controllers;

/// <summary>
/// Untuk mesin pencari (#42): halaman yang boleh dipindai (robots.txt) dan alamat versi Indonesia/Inggris
/// untuk halaman yang masuk indeks (sitemap.xml, hreflang). Halaman aplikasi dan tautan pelacakan pelanggan tidak dipindai.
/// </summary>
[AllowAnonymous]
public sealed class SeoController : Controller
{
    /// <summary>Halaman yang masuk indeks (Bahasa Indonesia di `/`; English di `/en`).</summary>
    private static readonly string[] Pages = ["/", "/privacy", "/terms"];

    // Uygulamanın kendisi, hesap sayfaları ve takip linkleri: oturum ister ya da kişiye özeldir.
    private static readonly string[] Private =
        ["/panel", "/work-orders", "/inventory", "/staff", "/settings", "/account/", "/invitation", "/demo", "/t/", "/language", "/error"];

    [HttpGet("/robots.txt")]
    public ContentResult Robots()
    {
        var text = new StringBuilder("User-agent: *\n");
        foreach (var path in Private)
            text.Append($"Disallow: {path}\nDisallow: {LanguagePrefix.English}{path}\n");
        text.Append($"\nSitemap: {Origin}/sitemap.xml\n");
        return Content(text.ToString(), "text/plain; charset=utf-8");
    }

    [HttpGet("/sitemap.xml")]
    public ContentResult Sitemap()
    {
        XNamespace sm = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XNamespace xhtml = "http://www.w3.org/1999/xhtml";
        var urls = Pages.SelectMany(page =>
        {
            var id = Origin + page;
            var en = Origin + LanguagePrefix.English + page;
            XElement[] alternates =
            [
                new(xhtml + "link", new XAttribute("rel", "alternate"), new XAttribute("hreflang", "id"), new XAttribute("href", id)),
                new(xhtml + "link", new XAttribute("rel", "alternate"), new XAttribute("hreflang", "en"), new XAttribute("href", en)),
                new(xhtml + "link", new XAttribute("rel", "alternate"), new XAttribute("hreflang", "x-default"), new XAttribute("href", id)),
            ];
            return new[] { id, en }.Select(loc => new XElement(sm + "url", new XElement(sm + "loc", loc), alternates));
        });
        var document = new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement(sm + "urlset", new XAttribute(XNamespace.Xmlns + "xhtml", xhtml), urls));
        return Content(document.Declaration + "\n" + document.Root, "application/xml; charset=utf-8");
    }

    // Yayında Caddy https ve alan adını iletir (ForwardedHeaders, AllowedHosts); adres isteğin kendisinden.
    private string Origin => $"{Request.Scheme}://{Request.Host}";
}
