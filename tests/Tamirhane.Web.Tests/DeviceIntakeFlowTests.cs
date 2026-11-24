using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tamirhane.Domain.Customers;
using Tamirhane.Web.Display;
using Tamirhane.Web.Localization;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>
/// TÃƒÂ¼re gÃƒÂ¶re cihaz kabulÃƒÂ¼: iÃ…Å¸aretlenen aksesuar ve durumlar metne ÃƒÂ§evrilir, tÃƒÂ¼rÃƒÂ¼n sormadÃ„Â±Ã„Å¸Ã„Â± alanlar (IMEI, ekran kilidi)
/// ve baÃ…Å¸ka tÃƒÂ¼rÃƒÂ¼n seÃƒÂ§enekleri kaydedilmez; model ÃƒÂ¶nerileri; takip sayfasÃ„Â±nÃ„Â±n web uygulamasÃ„Â± tanÃ„Â±mÃ„Â± (iPhone bildirimi).
/// </summary>
[Collection(WebCollection.Name)]
public sealed partial class DeviceIntakeFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Large_appliance_keeps_only_its_own_options_and_skips_imei_and_lock()
    {
        var owner = await _flows.OwnerAsync("tur-beyaz@example.com");

        var response = await ReceiveAsync(owner, "0812 3456 7890",
        [
            new("Input.DeviceKind", nameof(DeviceKind.LargeAppliance)), new("Input.Brand", "Arçelik"), new("Input.Model", "9 kg çamaşır"),
            new("Input.Imei", "490154203237518"),                  // beyaz eÃ…Å¸ya IMEI sormaz
            new("Input.AccessoryItems", "warranty_doc"), new("Input.AccessoryItems", "charger"), // Ã…Å¸arj aleti bu tÃƒÂ¼rde yok
            new("Input.Accessories", "giriş hortumu"),
            new("Input.ConditionItems", "water_leak"), new("Input.ConditionItems", "screen_cracked"), new("Input.ConditionItems", "<script>"),
            new("Input.LockType", "pin"), new("Input.Passcode", "1234"), // beyaz eÃ…Å¸ya kilit sormaz
        ]);

        var page = await BodyAsync(owner, Browser.RedirectOf(response));
        Assert.Contains("Peralatan besar", page);
        Assert.Contains("Dokumen garansi, giriş hortumu", page);
        Assert.Contains("Bocor air", page);
        Assert.DoesNotContain("Pengisi daya", page);
        Assert.DoesNotContain("Layar retak", page);
        Assert.DoesNotContain("490154203237518", page);
        Assert.DoesNotContain("Kode sandi perangkat tersimpan", page);
    }

    [DockerFact]
    public async Task Phone_lock_is_saved_with_its_type_and_none_saves_nothing()
    {
        var owner = await _flows.OwnerAsync("tur-telefon@example.com");

        var pin = await ReceiveAsync(owner, "0812 3456 7890",
        [
            new("Input.DeviceKind", nameof(DeviceKind.Phone)), new("Input.Brand", "Apple"), new("Input.Model", "iPhone 11"),
            new("Input.AccessoryItems", "case"), new("Input.AccessoryItems", "charger"),
            new("Input.LockType", "pattern"), new("Input.Passcode", "1-4-7-8-9"),
        ]);
        var none = await ReceiveAsync(owner, "0812 3456 7891",
        [
            new("Input.DeviceKind", nameof(DeviceKind.Phone)), new("Input.Brand", "Apple"), new("Input.Model", "iPhone 12"),
            new("Input.LockType", "none"), new("Input.Passcode", "unutulmuÃ…Å¸ deÃ„Å¸er"),
        ]);

        // Aksesuarlar katalog sÃ„Â±rasÃ„Â±yla (Ã…Å¸arj aleti kÃ„Â±lÃ„Â±ftan ÃƒÂ¶nce), Ã…Å¸ifre tÃƒÂ¼rÃƒÂ¼yle birlikte; sayfada ÃƒÂ§ÃƒÂ¶zÃƒÂ¼lmÃƒÂ¼Ã…Å¸, kayÃ„Â±tta Ã…Å¸ifreli.
        var pinPage = await BodyAsync(owner, Browser.RedirectOf(pin));
        Assert.Contains("Pengisi daya, Casing", pinPage);
        Assert.Contains("<code>Pola: 1-4-7-8-9</code>", pinPage);
        Assert.DoesNotContain("Tampilkan kode sandi perangkat", await BodyAsync(owner, Browser.RedirectOf(none)));
        var stored = Assert.Single(await PasscodesAsync("tur-telefon@example.com"));
        Assert.StartsWith("dp1:", stored);
        Assert.DoesNotContain("1-4-7-8-9", stored);
    }

    [DockerFact]
    public async Task Model_suggestions_mix_the_shops_history_with_the_built_in_list()
    {
        var owner = await _flows.OwnerAsync("tur-model@example.com");
        await ReceiveAsync(owner, "0812 3456 7890",
        [
            new("Input.DeviceKind", nameof(DeviceKind.LargeAppliance)), new("Input.Brand", "Arçelik"), new("Input.Model", "9 kg çamaşır"),
        ]);

        var history = await ModelsAsync(owner, "LargeAppliance", "arçelik");
        var builtIn = await ModelsAsync(owner, "Phone", "Apple");
        var otherKind = await ModelsAsync(owner, "Tv", "Arçelik");

        Assert.Equal(["9 kg çamaşır"], history);
        Assert.Contains("iPhone 11", builtIn);
        Assert.Empty(otherKind); // aynÃ„Â± marka, baÃ…Å¸ka tÃƒÂ¼r: ÃƒÂ¶neri yok
        Assert.StartsWith("/account/login", Browser.RedirectOf(await app.NewBrowser().GetAsync("/work-orders/device-models?kind=Phone&brand=Apple")));
    }

    [DockerFact]
    public async Task Tracking_page_links_its_own_web_app_manifest()
    {
        var owner = await _flows.OwnerAsync("tur-manifest@example.com");
        var order = await ShopFlows.ReceiveAsync(owner, "0812 3456 7890");
        var tracking = TrackingLink().Match(await BodyAsync(owner, order + "/receipt")).Groups[1].Value;
        var visitor = app.NewBrowser();

        var page = await BodyAsync(visitor, tracking);
        var response = await visitor.GetAsync(tracking + "/manifest.webmanifest");
        using var manifest = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Contains($"<link rel=\"manifest\" href=\"{tracking}/manifest.webmanifest\" />", page);
        Assert.Contains("rel=\"apple-touch-icon\"", page);
        Assert.Equal("application/manifest+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(tracking, manifest.RootElement.GetProperty("start_url").GetString());
        Assert.Equal("standalone", manifest.RootElement.GetProperty("display").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync("/t/7K3M9QX2HFDA/manifest.webmanifest")).StatusCode);
    }

    [Fact]
    public void Picked_options_become_text_in_catalog_order_and_unknown_keys_are_dropped()
    {
        var t = new JsonStringLocalizer(TextCatalog.Embedded);
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = AppCultures.Indonesian;
        try
        {
            var phone = DeviceCatalog.For(DeviceKind.Phone);
            Assert.Equal("Pengisi daya, Casing, pulpen merah",
                IntakeText.Join(t, "intake.acc.", ["case", "remote", "charger", "case"], phone.Accessories, " pulpen merah "));
            Assert.Null(IntakeText.Join(t, "intake.acc.", null, phone.Accessories, "  "));
            Assert.Equal("PIN: 0000", IntakeText.Passcode(t, phone, "pin", " 0000 "));
            Assert.Null(IntakeText.Passcode(t, phone, "fingerprint", "0000"));
            Assert.Null(IntakeText.Passcode(t, phone, "pin", " "));
            Assert.Equal("4826", IntakeText.Passcode(t, phone, null, "4826"));
            Assert.Null(IntakeText.Passcode(t, DeviceCatalog.For(DeviceKind.Tv), "pin", "1234"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Every_option_and_kind_has_text_in_both_languages()
    {
        var keys = DeviceCatalog.AllAccessories.Select(k => "intake.acc." + k)
            .Concat(DeviceCatalog.AllConditions.Select(k => "intake.cond." + k))
            .Concat(DeviceCatalog.LockTypes.Select(k => "intake.lock." + k));
        foreach (var language in new[] { "tr", "en" })
        foreach (var key in keys)
            Assert.True(TextCatalog.Embedded.Languages[language].ContainsKey(key), $"{language}: {key} yok");
        Assert.Equal(Enum.GetValues<DeviceKind>().Order(), DeviceCatalog.Kinds.Order()); // her tÃƒÂ¼r formda var
    }

    /// <summary>Kabul formunu gÃƒÂ¶nderir; aynÃ„Â± adlÃ„Â± alan birden ÃƒÂ§ok kez gidebilir (iÃ…Å¸aret kutularÃ„Â±).</summary>
    private static async Task<HttpResponseMessage> ReceiveAsync(Browser owner, string phone, List<KeyValuePair<string, string>> device)
    {
        var form = await BodyAsync(owner, "/work-orders/new?phone=" + Uri.EscapeDataString(phone));
        List<KeyValuePair<string, string>> fields =
        [
            new("__RequestVerificationToken", Token().Match(form).Groups[1].Value),
            new("Input.Phone", phone), new("Input.CustomerName", "Zeynep Arslan"), new("Input.KvkkConsent", "true"),
            new("Input.Complaint", "Ãƒâ€¡alÃ„Â±Ã…Å¸mÃ„Â±yor"),
            .. device,
        ];
        var response = await owner.PostAsync("/work-orders/new", new FormUrlEncodedContent(fields));
        if (response.StatusCode != HttpStatusCode.Redirect)
            Assert.Fail(Regex.Match(await response.Content.ReadAsStringAsync(), @"notice-error[^>]*>[\s\S]{0,400}").Value);
        return response;
    }

    private static async Task<List<string>> ModelsAsync(Browser owner, string kind, string brand)
    {
        var response = await owner.GetAsync($"/work-orders/device-models?kind={kind}&brand={Uri.EscapeDataString(brand)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonSerializer.Deserialize<List<string>>(await response.Content.ReadAsStringAsync())!;
    }

    private async Task<List<string?>> PasscodesAsync(string ownerEmail)
    {
        List<string?> passcodes = [];
        await app.AsShopAsync(Guid.Empty, async db =>
        {
            var userId = await db.Users.Where(u => u.Email == ownerEmail).Select(u => u.Id).SingleAsync();
            var shopId = await db.Staff.IgnoreQueryFilters().Where(s => s.UserId == userId).Select(s => s.ShopId).SingleAsync();
            passcodes = await db.WorkOrders.IgnoreQueryFilters().Where(o => o.ShopId == shopId && o.Passcode != null)
                .Select(o => o.Passcode).ToListAsync();
        });
        return passcodes;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex Token();

    [GeneratedRegex(@"https?://[^/""]+(/t/[0-9A-Z]{12})")]
    private static partial Regex TrackingLink();
}
