using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>
/// Takip sayfasÄ±ndan tarayÄ±cÄ± bildirimi (#31) HTTP Ã¼zerinden: dÃ¼ÄŸme, abone olma, durum deÄŸiÅŸince bildirim, Ã§Ä±kma.
/// TarayÄ±cÄ±nÄ±n JavaScript'i yerine istekler elle, aynÄ± biÃ§imde (JSON + CSRF baÅŸlÄ±ÄŸÄ±) gÃ¶nderilir.
/// </summary>
[Collection(WebCollection.Name)]
public sealed partial class PushFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Tracking_page_offers_notifications_with_the_public_key()
    {
        var (_, tracking) = await OrderAsync("push-sayfa@example.com", "0812 3456 7890");

        var page = await BodyAsync(app.NewBrowser(), tracking);

        Assert.Contains($"data-key=\"{TamirhaneWebFactory.VapidKeys.Public}\"", page);
        Assert.Contains("Beritahu saya saat status berubah", page);
        Assert.DoesNotContain(TamirhaneWebFactory.VapidKeys.Private, page);

        var worker = await app.NewBrowser().GetAsync("/sw.js");
        Assert.Equal(HttpStatusCode.OK, worker.StatusCode);
        Assert.Equal("text/javascript", worker.Content.Headers.ContentType?.MediaType);
    }

    [DockerFact]
    public async Task Subscribed_browser_gets_a_notification_when_the_status_changes_and_can_stop()
    {
        var (order, tracking) = await OrderAsync("push-akis@example.com", "0812 3456 7890");
        var customer = app.NewBrowser();
        using var browser = new TestBrowser($"https://fcm.googleapis.com/fcm/send/akis-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.NoContent, (await PostJsonAsync(customer, tracking, "/push", Subscription(browser))).StatusCode);
        Assert.True(await StateAsync(customer, tracking, browser));

        await StepAsync(_owner!, order, "StartDiagnosis");
        _ = app.Emails.Count; // arka plan kuyruÄŸu boÅŸalsÄ±n

        var payload = JsonDocument.Parse(browser.Decrypt(Assert.Single(app.Push.To(browser.Endpoint)).Body)).RootElement;
        Assert.Equal("Apple iPhone 13: sedang dicek", payload.GetProperty("title").GetString());
        Assert.EndsWith(tracking, payload.GetProperty("url").GetString());

        Assert.Equal(HttpStatusCode.NoContent,
            (await PostJsonAsync(customer, tracking, "/push/remove", new { endpoint = browser.Endpoint })).StatusCode);
        Assert.False(await StateAsync(customer, tracking, browser));
    }

    [DockerFact]
    public async Task A_subscription_pointing_elsewhere_is_refused()
    {
        var (_, tracking) = await OrderAsync("push-ssrf@example.com", "0812 3456 7890");
        var customer = app.NewBrowser();
        using var browser = new TestBrowser("https://169.254.169.254/latest/meta-data");

        Assert.Equal(HttpStatusCode.BadRequest, (await PostJsonAsync(customer, tracking, "/push", Subscription(browser))).StatusCode);
        Assert.False(await StateAsync(customer, tracking, browser));
    }

    [DockerFact]
    public async Task Requests_without_the_page_token_or_for_an_unknown_code_are_refused()
    {
        var (_, tracking) = await OrderAsync("push-csrf@example.com", "0812 3456 7890");
        using var browser = new TestBrowser($"https://fcm.googleapis.com/fcm/send/csrf-{Guid.NewGuid():N}");
        var customer = app.NewBrowser();
        await customer.GetAsync(tracking); // CSRF Ã§erezi olsun; baÅŸlÄ±k yine eksik

        var noToken = await customer.PostAsync(tracking + "/push", JsonContent.Create(Subscription(browser)));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);

        var unknown = await PostJsonAsync(customer, tracking, "/push", Subscription(browser), target: "/t/7K3M9QX2HFDA");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.False(await StateAsync(customer, tracking, browser));
    }

    [DockerFact]
    public async Task An_oversized_subscription_is_refused_and_nothing_is_stored()
    {
        var (_, tracking) = await OrderAsync("push-boyut@example.com", "0812 3456 7890");
        var customer = app.NewBrowser();
        using var browser = new TestBrowser("https://fcm.googleapis.com/fcm/send/" + new string('a', 10_000));

        var response = await PostJsonAsync(customer, tracking, "/push", Subscription(browser));

        // YayÄ±nda Kestrel 4 KB Ã¼stÃ¼ gÃ¶vdeyi hiÃ§ okumaz (413). Testteki bellek iÃ§i sunucu bu sÄ±nÄ±rÄ± uygulamaz;
        // orada da gÃ¶vde okunur ama domain uzun adresi reddeder (400). Ä°ki durumda da kayÄ±t yok.
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.BadRequest });
        Assert.False(await StateAsync(customer, tracking, browser));
    }

    private Browser? _owner;

    /// <summary>MÃ¼ÅŸteri e-postasÄ±z bir iÅŸ emri; dÃ¶nen: iÅŸ emri sayfasÄ± ve takip sayfasÄ±nÄ±n yolu.</summary>
    private async Task<(string Order, string Tracking)> OrderAsync(string ownerEmail, string phone)
    {
        _owner = await _flows.OwnerAsync(ownerEmail);
        var order = await ReceiveAsync(_owner, phone);
        var tracking = TrackingLink().Match(await BodyAsync(_owner, order + "/receipt")).Groups[1].Value;
        return (order, tracking);
    }

    private static object Subscription(TestBrowser browser) =>
        new { endpoint = browser.Endpoint, keys = new { p256dh = browser.P256dh, auth = browser.Auth } };

    /// <summary>tracking.js'in yaptÄ±ÄŸÄ± gibi: sayfadaki CSRF token'Ä± RequestVerificationToken baÅŸlÄ±ÄŸÄ±nda, gÃ¶vde JSON.</summary>
    private static async Task<HttpResponseMessage> PostJsonAsync(Browser customer, string tracking, string path, object body, string? target = null)
    {
        var page = await BodyAsync(customer, tracking);
        var token = Token().Match(page).Groups[1].Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, (target ?? tracking) + path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("RequestVerificationToken", token);
        return await customer.SendAsync(request);
    }

    private static async Task<bool> StateAsync(Browser customer, string tracking, TestBrowser browser)
    {
        var response = await PostJsonAsync(customer, tracking, "/push/state", new { endpoint = browser.Endpoint });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("subscribed").GetBoolean();
    }

    private static Task<HttpResponseMessage> StepAsync(Browser browser, string order, string step) =>
        browser.SubmitAsync(order, order + "/steps", new() { ["Step"] = step });

    [GeneratedRegex(@"https?://[^/""]+(/t/[0-9A-Z]{12})")]
    private static partial Regex TrackingLink();

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex Token();
}
