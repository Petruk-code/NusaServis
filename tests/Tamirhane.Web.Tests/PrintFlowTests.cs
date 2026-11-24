using System.Net;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>Kabul fiÃ…Å¸i, cihaz etiketi ve QR: iÃƒÂ§erik, gizlilik ve kiracÃ„Â± ayrÃ„Â±mÃ„Â±; HTTP ÃƒÂ¼zerinden.</summary>
[Collection(WebCollection.Name)]
public sealed class PrintFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Receipt_has_shop_customer_device_terms_signatures_and_the_tracking_qr()
    {
        var owner = await _flows.OwnerAsync("fis@example.com");
        var order = await ReceiveWithPasscodeAsync(owner, "0812 3456 7890");

        var details = await BodyAsync(owner, order);
        Assert.Contains($"href=\"{order}/receipt\"", details);
        Assert.Contains($"href=\"{order}/label\"", details);

        var receipt = await BodyAsync(owner, order + "/receipt");
        foreach (var expected in new[] { "Bengkel Jaya", "Merkez", "Tanda terima", "Zeynep Arslan", "0812 3456 7890", "Apple iPhone 13",
                     "Ekran kÃ„Â±rÃ„Â±k", "Kasada ÃƒÂ§izik", "KÃ„Â±lÃ„Â±f", "bergaransi 90 hari", "Diserahkan oleh:", "Diterima oleh: Ayşe Kaya",
                     $"src=\"{order}/qr.svg\"", "/t/", "Kode sandi perangkat dicatat" })
            Assert.Contains(expected, receipt);
        Assert.DoesNotContain("4826", receipt); // cihaz Ã…Å¸ifresinin kendisi fiÃ…Å¸te yok
        Assert.DoesNotContain("class=\"shell\"", receipt); // yazdÃ„Â±rma sayfasÃ„Â±nda menÃƒÂ¼ yok
    }

    [DockerFact]
    public async Task Label_shows_number_and_only_the_short_customer_name()
    {
        var owner = await _flows.OwnerAsync("etiket@example.com");
        var order = await ReceiveWithPasscodeAsync(owner, "0812 3456 7890");

        var label = await BodyAsync(owner, order + "/label");

        Assert.Matches(@"class=""label-no"">\d{4}-0001<", label);
        Assert.Contains("Zeynep A.", label);
        Assert.DoesNotContain("Zeynep Arslan", label);
        Assert.Contains($"src=\"{order}/qr.svg\"", label);
    }

    [DockerFact]
    public async Task Qr_is_an_svg_image_and_other_shops_get_nothing()
    {
        var owner = await _flows.OwnerAsync("qr-sahip@example.com");
        var order = await ReceiveWithPasscodeAsync(owner, "0812 3456 7890");

        var qr = await owner.GetAsync(order + "/qr.svg");
        Assert.Equal(HttpStatusCode.OK, qr.StatusCode);
        Assert.Equal("image/svg+xml", qr.Content.Headers.ContentType!.MediaType);
        var svg = await qr.Content.ReadAsStringAsync();
        Assert.Contains("<svg", svg);
        Assert.DoesNotContain("<script", svg);

        var attacker = await _flows.OwnerAsync("qr-saldirgan@example.com");
        foreach (var path in new[] { "/receipt", "/label", "/qr.svg" })
            Assert.Equal(HttpStatusCode.NotFound, (await attacker.GetAsync(order + path)).StatusCode);
        Assert.StartsWith("/account/login", Browser.RedirectOf(await app.NewBrowser().GetAsync(order + "/receipt")));
    }

    private static async Task<string> ReceiveWithPasscodeAsync(Browser browser, string phone)
    {
        var response = await browser.SubmitAsync($"/work-orders/new?phone={Uri.EscapeDataString(phone)}", "/work-orders/new", new()
        {
            ["Input.Phone"] = phone, ["Input.CustomerName"] = "Zeynep Arslan", ["Input.KvkkConsent"] = "true",
            ["Input.DeviceKind"] = "Phone", ["Input.Brand"] = "Apple", ["Input.Model"] = "iPhone 13",
            ["Input.Complaint"] = "Ekran kÃ„Â±rÃ„Â±k", ["Input.Condition"] = "Kasada ÃƒÂ§izik", ["Input.Accessories"] = "KÃ„Â±lÃ„Â±f", ["Input.Passcode"] = "4826",
        });
        return Browser.RedirectOf(response);
    }
}
