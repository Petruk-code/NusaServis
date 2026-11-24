using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>Tahsilat HTTP Ã¼zerinden: kÄ±smi Ã¶deme ve kalan borÃ§, fazla Ã¶deme, iade ve borÃ§lu teslimde sahip onayÄ±.</summary>
[Collection(WebCollection.Name)]
public sealed class PaymentFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Partial_payment_leaves_a_balance_and_overpayment_is_refused()
    {
        var owner = await _flows.OwnerAsync("odeme-kismi@example.com");
        var order = await QuotedAsync(owner, "0812 3456 7890");

        await PayAsync(owner, order, "500", "Cash", "Kapora");
        var page = await BodyAsync(owner, order);
        Assert.Contains("Pembayaran Rp 500 dicatat.", page);
        Assert.Contains("Sisa tagihan", page);
        Assert.Contains("Rp 700", page); // 1200 - 500
        Assert.Contains("Kapora", page);

        await PayAsync(owner, order, "800", "Card");
        Assert.Contains("Anda tidak bisa menerima lebih dari sisa tagihan.", await BodyAsync(owner, order));

        await PayAsync(owner, order, "700", "Card");
        page = await BodyAsync(owner, order);
        Assert.Contains("Lunas", page);
        Assert.DoesNotContain("Catat pembayaran", page);
    }

    [DockerFact]
    public async Task Delivering_with_a_balance_needs_the_owner()
    {
        var owner = await _flows.OwnerAsync("odeme-borclu@example.com");
        var technician = await _flows.TechnicianAsync(owner, "odeme-borclu-usta@example.com");
        var order = await ReadyAsync(owner, "0812 3456 7890");

        var techPage = await BodyAsync(technician, order);
        Assert.Contains("Sisa tagihan", techPage);
        Assert.DoesNotContain("Serahkan dengan sisa tagihan (persetujuan pemilik)", techPage); // seÃ§enek teknisyene gÃ¶rÃ¼nmez
        await StepAsync(technician, order, "Deliver", new() { ["AllowBalanceDue"] = "true" }); // elle gÃ¶nderse de olmaz
        Assert.Contains("Hanya pemilik bengkel", await BodyAsync(technician, order));

        await StepAsync(owner, order, "Deliver");
        Assert.Contains("Masih ada sisa tagihan. Catat pembayaran dulu", await BodyAsync(owner, order));

        await StepAsync(owner, order, "Deliver", new() { ["AllowBalanceDue"] = "true" });
        var page = await BodyAsync(owner, order);
        Assert.Contains("Perangkat telah diambil.", page);
        Assert.Contains("Diserahkan dengan sisa tagihan", page);
    }

    [DockerFact]
    public async Task Only_the_owner_refunds_and_cancel_needs_the_refund_first()
    {
        var owner = await _flows.OwnerAsync("odeme-iade@example.com");
        var technician = await _flows.TechnicianAsync(owner, "odeme-iade-usta@example.com");
        var order = await QuotedAsync(owner, "0812 3456 7890");
        await PayAsync(owner, order, "300", "Cash", "Kapora");

        Assert.DoesNotContain("Pengembalian dana", await BodyAsync(technician, order));
        await technician.SubmitAsync(order, order + "/refunds", new() { ["Amount"] = "300", ["Method"] = "Cash" });
        Assert.Contains("Hanya pemilik bengkel", await BodyAsync(technician, order));

        await StepAsync(owner, order, "Cancel", new() { ["Reason"] = "MÃ¼ÅŸteri vazgeÃ§ti" });
        Assert.Contains("Refund pembayaran yang diterima sebelum membatalkan.", await BodyAsync(owner, order));

        await owner.SubmitAsync(order, order + "/refunds", new() { ["Amount"] = "300", ["Method"] = "Cash", ["Note"] = "Kapora geri" });
        await StepAsync(owner, order, "Cancel", new() { ["Reason"] = "MÃ¼ÅŸteri vazgeÃ§ti" });
        var page = await BodyAsync(owner, order);
        Assert.Contains("Kapora geri", page);
        Assert.Contains("stamp stamp-warn is-current", page);
    }

    [DockerFact]
    public async Task Declined_quote_takes_a_diagnosis_fee()
    {
        var owner = await _flows.OwnerAsync("odeme-inceleme@example.com");
        var order = await QuotedAsync(owner, "0812 3456 7890");
        await StepAsync(owner, order, "SendQuote");
        await StepAsync(owner, order, "RecordDecline", new() { ["DecidedBy"] = "Zeynep Arslan" });

        await owner.SubmitAsync(order, order + "/diagnosis-fee", new() { ["fee"] = "150" });
        var page = await BodyAsync(owner, order);
        Assert.Contains("Biaya diagnosis disimpan.", page);
        Assert.Contains("Rp 150", page);

        await PayAsync(owner, order, "150", "Cash");
        await StepAsync(owner, order, "Deliver");
        Assert.Contains("Perangkat telah diambil.", await BodyAsync(owner, order));
    }

    private static async Task<string> QuotedAsync(Browser owner, string phone)
    {
        var order = await ReceiveAsync(owner, phone);
        await StepAsync(owner, order, "StartDiagnosis");
        await owner.SubmitAsync(order, order + "/quote-lines",
            new() { ["Kind"] = "Labor", ["Description"] = "Ekran deÄŸiÅŸimi", ["Quantity"] = "1", ["UnitPrice"] = "1000", ["VatRate"] = "20" });
        return order;
    }

    private static async Task<string> ReadyAsync(Browser owner, string phone)
    {
        var order = await QuotedAsync(owner, phone);
        await StepAsync(owner, order, "StartRepair");
        await StepAsync(owner, order, "MarkReady");
        return order;
    }

    private static Task<HttpResponseMessage> PayAsync(Browser browser, string order, string amount, string method, string? note = null) =>
        browser.SubmitAsync(order, order + "/payments", new() { ["Amount"] = amount, ["Method"] = method, ["Note"] = note ?? "" });

    private static Task<HttpResponseMessage> StepAsync(Browser browser, string order, string step, Dictionary<string, string>? extra = null)
    {
        var fields = extra ?? [];
        fields["Step"] = step;
        return browser.SubmitAsync(order, order + "/steps", fields);
    }
}
