using System.Globalization;

namespace Tamirhane.Web.Localization;

/// <summary>
/// Bahasa antarmuka: Indonesia (default) dan Inggris. Mata uang ditampilkan sebagai Rupiah (Rp, tanpa desimal)
/// di kedua bahasa karena bengkel target beroperasi di Indonesia.
/// </summary>
public static class AppCultures
{
    public const string CookieName = "__Host-tamirhane-lang";

    public static readonly CultureInfo Indonesian = CreateIndonesian();
    public static readonly CultureInfo English = CreateEnglish();
    public static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static readonly IReadOnlyList<CultureInfo> All = [Indonesian, English];

    /// <summary>"id", "en", "id-ID", "en-US"...; nilai tak dikenal null. "tr" tetap dikenali untuk kompatibilitas data lama.</summary>
    public static CultureInfo? From(string? value)
    {
        var code = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(code))
            return null;
        if (code == "id" || code.StartsWith("id-", StringComparison.Ordinal))
            return Indonesian;
        if (code == "en" || code.StartsWith("en-", StringComparison.Ordinal))
            return English;
        if (code == "tr" || code.StartsWith("tr-", StringComparison.Ordinal))
            return Turkish;
        return null;
    }

    /// <summary>
    /// Dari sini, teks/tanggal/mata uang memakai budaya yang diberikan. Perubahan tetap dalam async flow,
    /// sehingga bisa dipakai untuk merender tampilan tertentu tanpa mengubah layout.
    /// </summary>
    public static void Use(CultureInfo culture)
    {
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private static CultureInfo CreateIndonesian()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("id-ID").Clone();
        culture.NumberFormat.CurrencySymbol = "Rp ";
        culture.NumberFormat.CurrencyDecimalDigits = 0;
        culture.NumberFormat.CurrencyGroupSeparator = ".";
        culture.NumberFormat.CurrencyDecimalSeparator = ",";
        return CultureInfo.ReadOnly(culture);
    }

    private static CultureInfo CreateEnglish()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-GB").Clone();
        culture.NumberFormat.CurrencySymbol = "Rp ";
        culture.NumberFormat.CurrencyDecimalDigits = 0;
        culture.NumberFormat.CurrencyGroupSeparator = ".";
        culture.NumberFormat.CurrencyDecimalSeparator = ",";
        return CultureInfo.ReadOnly(culture);
    }
}
