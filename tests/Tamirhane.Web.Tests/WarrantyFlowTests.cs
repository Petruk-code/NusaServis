using System.Net;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>Garanti dÃ¶nÃ¼ÅŸÃ¼ (#34) HTTP Ã¼zerinden: teslim edilmiÅŸ iÅŸten yeni kayÄ±t, iki yÃ¶nlÃ¼ baÄŸ, iÅŸÃ§ilik Ã¼cretsiz Ã¶nerilir.</summary>
[Collection(WebCollection.Name)]
public sealed class WarrantyFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Delivered_order_opens_a_linked_warranty_order_with_free_labor_suggested()
    {
        var owner = await _flows.OwnerAsync("garanti-akis@example.com");
        var previous = await DeliveredAsync(owner, "0812 3456 7890");
        Assert.Contains(">Garansi<", await BodyAsync(owner, previous));

        var response = await owner.SubmitAsync(previous + "/warranty", previous + "/warranty", new()
        {
            ["Input.Complaint"] = "DeÄŸiÅŸen ekranda dokunmatik yine Ã§alÄ±ÅŸmÄ±yor",
        });
        var current = Browser.RedirectOf(response);
        Assert.NotEqual(previous, current);

        var page = await BodyAsync(owner, current);
        Assert.Contains("Came back under warranty. Previous order servis:", page);
        Assert.Contains($"href=\"{previous}\"", page);

        await Step(owner, current, "StartDiagnosis"); // teklif formu inceleme sÄ±rasÄ±nda aÃ§Ä±lÄ±r
        page = await BodyAsync(owner, current);
        Assert.Contains("Garansi: biasanya gratis kecuali ada biaya tambahan.", page);
        Assert.Matches("id=\"free-price\"[^>]*value=\"0\"", page);

        var back = await BodyAsync(owner, previous);
        Assert.Contains("Kembali dalam garansi:", back);
        Assert.Contains($"href=\"{current}\"", back);
    }

    [DockerFact]
    public async Task Complaint_is_required()
    {
        var owner = await _flows.OwnerAsync("garanti-bos@example.com");
        var previous = await DeliveredAsync(owner, "0812 3456 7890");

        var response = await owner.SubmitAsync(previous + "/warranty", previous + "/warranty", new() { ["Input.Complaint"] = "" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Masukkan keluhan pelanggan.", await response.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task An_open_order_offers_no_warranty_record()
    {
        var owner = await _flows.OwnerAsync("garanti-acik@example.com");
        var order = await ReceiveAsync(owner, "0812 3456 7890");

        Assert.DoesNotContain(">Garansi<", await BodyAsync(owner, order));
        Assert.Equal(order, Browser.RedirectOf(await owner.GetAsync(order + "/warranty")));
    }

    private static async Task<string> DeliveredAsync(Browser owner, string phone)
    {
        var order = await ReceiveAsync(owner, phone);
        foreach (var step in new[] { "StartDiagnosis", "StartRepair", "MarkReady", "Deliver" })
            await Step(owner, order, step);
        return order;
    }

    private static Task<HttpResponseMessage> Step(Browser owner, string order, string step) =>
        owner.SubmitAsync(order, order + "/steps", new() { ["Step"] = step });
}
