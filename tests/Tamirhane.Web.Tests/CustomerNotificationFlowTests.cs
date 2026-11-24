using System.Net;
using System.Text.RegularExpressions;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>MÃƒÂ¼Ã…Å¸teriye durum e-postasÃ„Â± (#30) uygulamanÃ„Â±n tamamÃ„Â±nda: her adÃ„Â±mda bir e-posta, link takip sayfasÃ„Â±nÃ„Â± aÃƒÂ§ar,
/// e-posta servisi ÃƒÂ§ÃƒÂ¶kÃƒÂ¼kken iÃ…Å¸ emri yine ilerler.</summary>
[Collection(WebCollection.Name)]
public sealed partial class CustomerNotificationFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Customer_gets_an_email_at_every_step_with_a_working_tracking_link()
    {
        var owner = await _flows.OwnerAsync("bildirim-akis@example.com");
        var order = await ReceiveWithEmailAsync(owner, "0812 3456 7890", "musteri-akis@example.com");

        foreach (var step in new[] { "StartDiagnosis", "StartRepair", "MarkReady" })
            await StepAsync(owner, order, step);

        var mails = app.Emails.SentTo("musteri-akis@example.com");
        Assert.Equal(
            ["Apple iPhone 13: unit diterima", "Apple iPhone 13: sedang dicek", "Apple iPhone 13: sedang diperbaiki", "Apple iPhone 13: siap diambil"],
            mails.Select(m => m.Subject));

        var link = new Uri(TrackingLink().Match(mails[^1].Text).Value).PathAndQuery;
        var page = await app.NewBrowser().GetAsync(link); // oturumsuz, baÃ…Å¸ka bir tarayÃ„Â±cÃ„Â±dan
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Siap diambil", await page.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task Work_order_moves_on_even_when_the_email_service_is_down()
    {
        var owner = await _flows.OwnerAsync("bildirim-hata@example.com");
        var order = await ReceiveWithEmailAsync(owner, "0812 3456 7890", "musteri-hata@example.com");

        app.Emails.Failing = true;
        try
        {
            Assert.Equal(order, Browser.RedirectOf(await StepAsync(owner, order, "StartDiagnosis")));
            Assert.Contains(">Pengecekan / Diagnosa<", await BodyAsync(owner, order));
            _ = app.Emails.Count; // kuyruk boÃ…Å¸alsÃ„Â±n: gÃƒÂ¶nderim denemeleri bitti, hata iÃ…Å¸i bozmadÃ„Â±
        }
        finally
        {
            app.Emails.Failing = false;
        }

        Assert.DoesNotContain(app.Emails.SentTo("musteri-hata@example.com"), m => m.Subject.EndsWith("sedang dicek"));
    }

    private static async Task<string> ReceiveWithEmailAsync(Browser owner, string phone, string email)
    {
        var response = await owner.SubmitAsync($"/work-orders/new?phone={Uri.EscapeDataString(phone)}", "/work-orders/new", new()
        {
            ["Input.Phone"] = phone, ["Input.CustomerName"] = "Zeynep Arslan", ["Input.CustomerEmail"] = email,
            ["Input.KvkkConsent"] = "true", ["Input.DeviceKind"] = "Phone", ["Input.Brand"] = "Apple", ["Input.Model"] = "iPhone 13",
            ["Input.Complaint"] = "Ekran kÃ„Â±rÃ„Â±k",
        });
        return Browser.RedirectOf(response);
    }

    private static Task<HttpResponseMessage> StepAsync(Browser browser, string order, string step) =>
        browser.SubmitAsync(order, order + "/steps", new() { ["Step"] = step });

    [GeneratedRegex(@"https?://\S+/t/\S+")]
    private static partial Regex TrackingLink();
}
