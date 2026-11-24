using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace Tamirhane.Web.Localization;

/// <summary>Sumber tunggal untuk semua teks antarmuka (view, controller, pesan validasi).</summary>
public sealed class SharedResource;

/// <summary>
/// Resources/id.json, en.json, tr.json: kamus datar { "key": "text" } yang di-embed ke assembly.
/// Dibaca sekali saat aplikasi mulai.
/// </summary>
public sealed class TextCatalog
{
    public static readonly TextCatalog Embedded = Load();

    private TextCatalog(FrozenDictionary<string, FrozenDictionary<string, string>> languages) => Languages = languages;

    /// <summary>Kode bahasa ("id", "en", "tr") → key → teks.</summary>
    public FrozenDictionary<string, FrozenDictionary<string, string>> Languages { get; }

    public string? Find(string language, string key) =>
        Languages.TryGetValue(language, out var texts) && texts.TryGetValue(key, out var text) ? text : null;

    public static TextCatalog FromJson(IReadOnlyDictionary<string, string> jsonByLanguage) =>
        new(jsonByLanguage.ToFrozenDictionary(
            p => p.Key,
            p => (JsonSerializer.Deserialize<Dictionary<string, string>>(p.Value) ?? []).ToFrozenDictionary(StringComparer.Ordinal)));

    private static TextCatalog Load()
    {
        var assembly = typeof(TextCatalog).Assembly;
        var json = new Dictionary<string, string>();
        foreach (var language in new[] { "id", "en", "tr" })
        {
            using var stream = assembly.GetManifestResourceStream($"Tamirhane.Web.Resources.{language}.json")
                ?? throw new InvalidOperationException($"Resources/{language}.json tidak ter-embed di assembly.");
            using var reader = new StreamReader(stream);
            json[language] = reader.ReadToEnd();
        }
        return FromJson(json);
    }
}

/// <summary>
/// Teks berdasarkan bahasa UI saat ini. Fallback: id -> en -> tr -> key.
/// </summary>
public sealed class JsonStringLocalizer(TextCatalog catalog) : IStringLocalizer
{
    public LocalizedString this[string name]
    {
        get
        {
            var (text, found) = Lookup(name);
            return new LocalizedString(name, text, resourceNotFound: !found);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var (text, found) = Lookup(name);
            return new LocalizedString(name, string.Format(CultureInfo.CurrentCulture, text, arguments), resourceNotFound: !found);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        catalog.Languages.TryGetValue(Language, out var texts)
            ? texts.Select(p => new LocalizedString(p.Key, p.Value))
            : [];

    private static string Language
    {
        get
        {
            var code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return code is "id" or "en" or "tr" ? code : "id";
        }
    }

    private (string Text, bool Found) Lookup(string name)
    {
        foreach (var language in new[] { Language, "id", "en", "tr" }.Distinct(StringComparer.Ordinal))
        {
            if (catalog.Find(language, name) is { } text)
                return (text, true);
        }
        return (name, false);
    }
}

/// <summary>Localizer bersama untuk semua tipe/lokasi.</summary>
public sealed class JsonStringLocalizerFactory(TextCatalog catalog) : IStringLocalizerFactory
{
    private readonly JsonStringLocalizer _localizer = new(catalog);

    public IStringLocalizer Create(Type resourceSource) => _localizer;

    public IStringLocalizer Create(string baseName, string location) => _localizer;
}
