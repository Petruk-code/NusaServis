using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Tamirhane.Web.Localization;

namespace Tamirhane.Web.Tests;

/// <summary>
/// Teks UI: id/en/tr punya key dan placeholder sama, fallback, format Rupiah, prefix bahasa.
/// </summary>
public sealed partial class LocalizationTests
{
    private static readonly TextCatalog Catalog = TextCatalog.Embedded;

    [Fact]
    public void All_language_files_have_the_same_keys()
    {
        var english = Catalog.Languages["en"].Keys.ToHashSet();
        foreach (var lang in new[] { "id", "tr" })
        {
            var keys = Catalog.Languages[lang].Keys.ToHashSet();
            Assert.Empty(keys.Except(english));
            Assert.Empty(english.Except(keys));
        }
    }

    [Fact]
    public void Every_key_has_the_same_placeholders_in_all_languages()
    {
        var mismatched = new List<string>();
        foreach (var lang in new[] { "id", "tr" })
        foreach (var p in Catalog.Languages["en"])
        {
            if (Catalog.Languages[lang].TryGetValue(p.Key, out var text)
                && !Placeholders(p.Value).SetEquals(Placeholders(text)))
                mismatched.Add($"{lang}:{p.Key}");
        }

        Assert.Empty(mismatched);
    }

    [Fact]
    public void No_text_is_empty_and_keys_are_dotted_lowercase_names()
    {
        foreach (var (language, texts) in Catalog.Languages)
        foreach (var (key, text) in texts)
        {
            Assert.False(string.IsNullOrWhiteSpace(text), $"{language}: {key} boş");
            Assert.Matches(KeyPattern(), key);
        }
    }

    [Fact]
    public void Text_comes_in_the_current_language_and_falls_back_to_indonesian_then_to_the_key()
    {
        var catalog = TextCatalog.FromJson(new Dictionary<string, string>
        {
            ["id"] = """{ "a": "Halo {0}", "only.id": "Hanya Indonesia" }""",
            ["en"] = """{ "a": "Hello {0}" }""",
            ["tr"] = """{ "a": "Merhaba {0}" }""",
        });
        var localizer = new JsonStringLocalizer(catalog);

        using (UiCulture(AppCultures.English))
        {
            Assert.Equal("Hello Ayşe", localizer["a", "Ayşe"].Value);
            Assert.Equal("Hanya Indonesia", localizer["only.id"].Value);
            var missing = localizer["missing.key"];
            Assert.Equal("missing.key", missing.Value);
            Assert.True(missing.ResourceNotFound);
        }
        using (UiCulture(AppCultures.Indonesian))
            Assert.Equal("Halo Ayşe", localizer["a", "Ayşe"].Value);
    }

    [Fact]
    public void Prices_are_in_rupiah_in_supported_languages()
    {
        Assert.Equal(1200.50m.ToString("C", AppCultures.English), 1200.50m.ToString("C", AppCultures.Indonesian));
        Assert.StartsWith("Rp", 1200m.ToString("C", AppCultures.Indonesian));
    }

    [Theory]
    [InlineData("/en", "en-GB")]
    [InlineData("", "id-ID")]
    [InlineData("/eng", "id-ID")]
    public async Task Language_comes_from_the_address_prefix(string pathBase, string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.PathBase = pathBase;
        context.Request.Headers.AcceptLanguage = "de-DE,en;q=0.9";

        var result = await new LanguagePrefix.Provider().DetermineProviderCultureResult(context);

        Assert.Equal(expected, result?.Cultures.Single().Value);
    }

    [Theory]
    [InlineData("en", "en-GB")]
    [InlineData("EN-us", "en-GB")]
    [InlineData("id", "id-ID")]
    [InlineData("tr", "tr-TR")]
    [InlineData("de", null)]
    [InlineData(null, null)]
    public void Language_codes_map_to_supported_cultures(string? code, string? expected)
    {
        Assert.Equal(expected, AppCultures.From(code)?.Name);
    }

    private static HashSet<string> Placeholders(string text) => Placeholder().Matches(text).Select(m => m.Value).ToHashSet();

    private static IDisposable UiCulture(CultureInfo culture) => new CultureScope(culture);

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentUICulture;

        public CultureScope(CultureInfo culture) => CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = culture;

        public void Dispose() => CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = _previous;
    }

    [GeneratedRegex(@"\{\d+(:[^}]*)?\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"^[a-z0-9]+(\.[a-z0-9_-]+)+$")]
    private static partial Regex KeyPattern();
}
