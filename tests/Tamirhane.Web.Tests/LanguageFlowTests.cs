using System.Net;

namespace Tamirhane.Web.Tests;

/// <summary>
/// Language prefix: default Indonesian (no prefix), English /en. Choice is stored in cookie and only local redirects.
/// </summary>
[Collection(WebCollection.Name)]
public sealed class LanguageFlowTests(TamirhaneWebFactory app)
{
    [DockerFact]
    public async Task Choosing_english_moves_to_the_en_address_and_is_remembered()
    {
        var browser = app.NewBrowser();

        var response = await browser.SubmitAsync("/account/login", "/language",
            new() { ["culture"] = "en", ["returnUrl"] = "/account/login" });

        Assert.Equal("/en/account/login", Browser.RedirectOf(response));
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-tamirhane-lang="));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("/en/account/login?ReturnUrl=%2Fpanel", Browser.RedirectOf(await browser.GetAsync("/account/login?ReturnUrl=%2Fpanel")));
        var page = await browser.GetAsync("/en/account/login");
        Assert.Equal("en-GB", page.Content.Headers.ContentLanguage.Single());
    }

    [DockerFact]
    public async Task The_address_decides_not_the_browser_language()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/account/login");
        request.Headers.AcceptLanguage.ParseAdd("en-US");

        var indonesian = await app.NewBrowser().SendAsync(request);
        var english = await app.NewBrowser().GetAsync("/en/account/login");

        Assert.Equal(HttpStatusCode.OK, indonesian.StatusCode);
        Assert.Equal("id-ID", indonesian.Content.Headers.ContentLanguage.Single());
        Assert.Equal("en-GB", english.Content.Headers.ContentLanguage.Single());
    }

    [DockerFact]
    public async Task Indonesian_is_the_default()
    {
        var page = await app.NewBrowser().GetAsync("/account/login");

        Assert.Equal("id-ID", page.Content.Headers.ContentLanguage.Single());
    }

    [DockerFact]
    public async Task Choosing_indonesian_on_an_english_page_returns_to_the_plain_address()
    {
        var browser = await EnglishAsync(app.NewBrowser(), "/account/login");

        var response = await browser.SubmitAsync("/en/account/login", "/en/language",
            new() { ["culture"] = "id", ["returnUrl"] = "/account/login" });

        Assert.Equal("/account/login", Browser.RedirectOf(response));
        var page = await browser.GetAsync("/account/login");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("id-ID", page.Content.Headers.ContentLanguage.Single());
    }

    [DockerFact]
    public async Task It_never_redirects_outside_the_site()
    {
        var response = await app.NewBrowser().SubmitAsync("/account/login", "/language",
            new() { ["culture"] = "en", ["returnUrl"] = "https://evil.example/phish" });

        Assert.Equal("/en/", Browser.RedirectOf(response));
    }

    [DockerFact]
    public async Task Sign_in_page_texts_errors_and_links_follow_the_language()
    {
        var browser = app.NewBrowser();

        var page = await (await browser.GetAsync("/en/account/login")).Content.ReadAsStringAsync();
        Assert.Contains("<html lang=\"en\">", page);
        Assert.Contains("<h1>Sign in</h1>", page);
        Assert.Contains(">Indonesia</button>", page);
        Assert.Contains("action=\"/en/account/login\"", page);
        Assert.Contains("href=\"/en/account/register\"", page);
        Assert.DoesNotContain(">Masuk</h1>", page);

        var empty = await browser.SubmitAsync("/en/account/login", "/en/account/login", new() { ["Email"] = "", ["Password"] = "x" });
        Assert.Contains("Enter your email address.", await empty.Content.ReadAsStringAsync());
        var wrong = await browser.SubmitAsync("/en/account/login", "/en/account/login",
            new() { ["Email"] = "yok-boyle-biri@example.com", ["Password"] = "yanlis-sifre-123" });
        Assert.Contains("Wrong email or password.", await wrong.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task The_panel_switches_language_for_this_person_only()
    {
        var flows = new ShopFlows(app);
        var owner = await flows.OwnerAsync("dil-sahibi@example.com");
        await EnglishAsync(owner, "/panel");

        Assert.Equal("/en/panel", Browser.RedirectOf(await owner.GetAsync("/panel")));
        var english = await ShopFlows.BodyAsync(owner, "/en/panel");
        Assert.Contains("<h1>Today</h1>", english);
        Assert.Contains(">Dashboard</a>", english);
        Assert.Contains(">Sign out</button>", english);

        var other = await flows.OwnerAsync("dil-sahibi-2@example.com");
        Assert.Contains("<h1>Dasbor</h1>", await ShopFlows.BodyAsync(other, "/panel"));
    }

    [DockerFact]
    public async Task Work_order_list_and_intake_follow_the_language()
    {
        var flows = new ShopFlows(app);
        var owner = await flows.OwnerAsync("dil-is-emri@example.com");
        var order = await ShopFlows.ReceiveAsync(owner, "0812 2110 0101");
        await EnglishAsync(owner, "/panel");

        var list = await ShopFlows.BodyAsync(owner, "/en/work-orders");
        foreach (var expected in new[] { ">Checked in<", ">Open</a>", ">Over 30 days</a>", "today", "Ekran kÄ±rÄ±k", "href=\"/en" + order + "\"" })
            Assert.Contains(expected, list);
        Assert.DoesNotContain("Unit diterima", list);

        var intake = await ShopFlows.BodyAsync(owner, "/en/work-orders/new?phone=12345");
        Assert.Contains("Enter the phone like 0812 3456 7890.", intake);
        Assert.Contains("Find customer", intake);
        Assert.Contains(">Check in a device</h1>", await ShopFlows.BodyAsync(owner, "/en/work-orders/new"));
    }

    [DockerFact]
    public async Task Work_order_page_follows_the_staff_language_but_the_receipt_uses_the_customer_language()
    {
        var flows = new ShopFlows(app);
        var owner = await flows.OwnerAsync("dil-fis@example.com");
        var order = await ShopFlows.ReceiveAsync(owner, "0812 2120 0101");
        await EnglishAsync(owner, "/panel");

        var page = await ShopFlows.BodyAsync(owner, "/en" + order);
        foreach (var expected in new[] { ">Next step</h2>", ">Start diagnosis</button>", ">Quote</h2>", ">Payments</h2>", ">Print receipt</a>" })
            Assert.Contains(expected, page);
        Assert.Contains("hx-post=\"/en" + order + "/steps\"", page);

        var receipt = await ShopFlows.BodyAsync(owner, "/en" + order + "/receipt");
        Assert.Contains("<article class=\"sheet receipt\" lang=\"id\"", receipt);
        Assert.Contains("<span>Nama</span>", receipt);
        Assert.Contains("Diserahkan oleh: Zeynep Arslan", receipt);
        Assert.Contains(">Back to the work order</a>", receipt);
        Assert.Contains("<html lang=\"en\">", receipt);
    }

    [DockerFact]
    public async Task Inventory_staff_and_settings_follow_the_language()
    {
        var flows = new ShopFlows(app);
        var owner = await flows.OwnerAsync("dil-ayarlar@example.com");
        await EnglishAsync(owner, "/panel");

        Assert.Contains(">New part</h2>", await ShopFlows.BodyAsync(owner, "/en/inventory"));
        var part = await owner.SubmitAsync("/en/inventory", "/en/inventory", new() { ["NewPart.Sku"] = "", ["NewPart.Name"] = "Layar" });
        Assert.Contains("Enter the part code (e.g. IP13-SCR).", await part.Content.ReadAsStringAsync());

        Assert.Contains(">Invite a technician</h2>", await ShopFlows.BodyAsync(owner, "/en/staff"));

        var settings = await owner.SubmitAsync("/en/settings", "/en/settings", new()
        {
            ["Shop.Name"] = "Bengkel Jaya", ["Shop.WarrantyDays"] = "5000", ["Shop.DefaultVatRate"] = "20", ["Shop.Locale"] = "id",
        });
        var html = await settings.Content.ReadAsStringAsync();
        Assert.Contains("The warranty period must be between 0 and 3650 days.", html);
        Assert.Contains(">Save settings</button>", html);
    }

    [DockerFact]
    public async Task Emails_to_an_english_shop_link_to_english_pages()
    {
        var browser = app.NewBrowser();
        await browser.SubmitAsync("/en/account/register", "/en/account/register", new()
        {
            ["ShopName"] = "Grant Repairs", ["OwnerName"] = "Oliver Grant", ["Email"] = "dil-eposta@example.com",
            ["Password"] = ShopFlows.OwnerPassword, ["Locale"] = "en",
        });

        Assert.Contains("/en/account/confirm-email?", new ShopFlows(app).LinkIn("dil-eposta@example.com", "/account/confirm-email"));
    }

    [DockerFact]
    public async Task Cookies_on_english_pages_use_the_root_path()
    {
        var browser = app.NewBrowser();
        var page = await browser.GetAsync("/en/account/login");
        var pageCookies = page.Headers.TryGetValues("Set-Cookie", out var initial) ? initial.ToList() : [];
        var login = await browser.SubmitAsync("/en/account/login", "/en/account/login",
            new() { ["Email"] = "yok-boyle-biri@example.com", ["Password"] = "yanlis-sifre-123" });

        var cookies = (login.Headers.TryGetValues("Set-Cookie", out var more) ? more.ToList() : [])
            .Concat(pageCookies).ToList();
        Assert.Contains(cookies, c => c.StartsWith("__Host-tamirhane-csrf="));
        foreach (var cookie in cookies.Where(c => c.StartsWith("__Host-")))
            Assert.Contains("path=/;", cookie.ToLowerInvariant() + ";");
        Assert.DoesNotContain(cookies, c => c.Contains("path=/en", StringComparison.OrdinalIgnoreCase));
    }

    [DockerFact]
    public async Task An_unknown_language_changes_nothing()
    {
        var response = await app.NewBrowser().SubmitAsync("/account/login", "/language",
            new() { ["culture"] = "de", ["returnUrl"] = "/account/login" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie") &&
                     response.Headers.GetValues("Set-Cookie").Any(c => c.StartsWith("__Host-tamirhane-lang=")));
    }

    private static async Task<Browser> EnglishAsync(Browser browser, string page)
    {
        var response = await browser.SubmitAsync(page, "/language", new() { ["culture"] = "en", ["returnUrl"] = "/" });
        Assert.Equal("/en/", Browser.RedirectOf(response));
        return browser;
    }
}
