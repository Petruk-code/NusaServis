using System.Net;
using System.Text.RegularExpressions;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>"WhatsApp'tan bildir" (#32) iÅŸ emri sayfasÄ±nda: wa.me linki, gÃ¼ncel durumun mesajÄ±, htmx adÄ±mÄ±ndan sonra da gÃ¼ncel.</summary>
[Collection(WebCollection.Name)]
public sealed partial class WhatsAppFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Work_order_page_opens_whatsapp_with_the_status_and_the_tracking_link()
    {
        var owner = await _flows.OwnerAsync("whatsapp-sayfa@example.com");
        var order = await ReceiveAsync(owner, "0812 3456 7890");

        var page = await BodyAsync(owner, order);
        var (link, text) = WhatsApp(page);

        Assert.StartsWith("https://wa.me/6281234567890?text=", link);
        Assert.Contains("target=\"_blank\" rel=\"noopener noreferrer\"", page);
        Assert.StartsWith("Halo Zeynep,\nPerangkat Anda sudah diterima di bengkel.", text);
        Assert.Matches(@"Lacak servis Anda: https?://\S+/t/[0-9A-Z]{12}$", text);
    }

    [DockerFact]
    public async Task After_a_step_the_message_tells_the_new_status()
    {
        var owner = await _flows.OwnerAsync("whatsapp-adim@example.com");
        var order = await ReceiveAsync(owner, "0812 3456 7890");

        // Sayfa yenilenmeden (htmx) adÄ±m ilerler; dÃ¶nen parÃ§adaki link yeni durumu sÃ¶ylemeli.
        var response = await owner.SubmitAsync(order, order + "/steps", new() { ["Step"] = "StartDiagnosis" },
            new() { ["HX-Request"] = "true" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (_, text) = WhatsApp(await response.Content.ReadAsStringAsync());
        Assert.Contains("Perangkat Anda sedang dicek/didiagnosa.", text);
    }

    private static (string Link, string Text) WhatsApp(string html)
    {
        var link = WebUtility.HtmlDecode(WhatsAppLink().Match(html).Groups[1].Value);
        Assert.False(string.IsNullOrEmpty(link), "Sayfada WhatsApp linki yok.");
        return (link, Uri.UnescapeDataString(new Uri(link).Query["?text=".Length..]));
    }

    [GeneratedRegex("href=\"(https://wa\\.me/[^\"]+)\"")]
    private static partial Regex WhatsAppLink();
}
