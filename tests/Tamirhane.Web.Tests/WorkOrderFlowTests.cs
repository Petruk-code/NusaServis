using System.Net;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>Ä°ÅŸ emri sayfasÄ± HTTP Ã¼zerinden: sÄ±radaki adÄ±m, geÃ§ersiz geÃ§iÅŸ, htmx ile kÄ±smi gÃ¼ncelleme, iptal, notlar.</summary>
[Collection(WebCollection.Name)]
public sealed class WorkOrderFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Order_moves_from_received_to_delivered_one_step_at_a_time()
    {
        var owner = await _flows.OwnerAsync("is-akis@example.com");
        var order = await ReceiveAsync(owner, "0812 3456 7890");

        var page = await BodyAsync(owner, order);
        Assert.Contains(">Mulai pengecekan<", page);
        Assert.DoesNotContain(">Tandai siap diambil<", page); // geÃ§ersiz geÃ§iÅŸ gÃ¶rÃ¼nmez

        foreach (var step in new[] { "StartDiagnosis", "StartRepair", "MarkReady", "Deliver" })
            Assert.Equal(order, Browser.RedirectOf(await StepAsync(owner, order, step)));

        page = await BodyAsync(owner, order);
        Assert.Contains("Perangkat telah diambil.", page);
        Assert.Contains("Garansi", page);
        Assert.DoesNotContain("Langkah berikutnya", page);
        foreach (var stamp in new[] { "Diterima", "Pengecekan / Diagnosa", "Dalam perbaikan", "Siap diambil", "Sudah diserahkan" })
            Assert.Contains(stamp, page);
    }

    [DockerFact]
    public async Task Invalid_step_sent_by_hand_is_refused_and_nothing_changes()
    {
        var owner = await _flows.OwnerAsync("is-gecersiz@example.com");
        var order = await ReceiveAsync(owner, "0812 3456 7890");

        await StepAsync(owner, order, "MarkReady");

        var page = await BodyAsync(owner, order);
        Assert.Contains("Tindakan ini tidak bisa dilakukan pada status order servis saat ini", page);
        Assert.Contains(">Mulai pengecekan<", page);
        Assert.DoesNotContain(">Dalam perbaikan<", page);
    }

    [DockerFact]
    public async Task Htmx_step_returns_only_the_work_order_part_with_a_freshly_pressed_stamp()
    {
        var owner = await _flows.OwnerAsync("is-htmx@example.com");
        var order = await ReceiveAsync(owner, "0812 3456 7890");

        var response = await owner.SubmitAsync(order, order + "/steps", new() { ["Step"] = "StartDiagnosis" },
            new() { ["HX-Request"] = "true" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("<div id=\"work-order\">", html.TrimStart());
        Assert.DoesNotContain("<html", html);
        Assert.Contains("stamp is-current is-pressed\">Pengecekan / Diagnosa<", html);
        Assert.Contains("Status diperbarui: Pengecekan / Diagnosa.", html);
    }

    [DockerFact]
    public async Task Cancel_needs_a_reason_that_then_shows_in_the_history()
    {
        var owner = await _flows.OwnerAsync("is-iptal@example.com");
        var order = await ReceiveAsync(owner, "0812 3456 7890");

        await StepAsync(owner, order, "Cancel");
        Assert.Contains("Masukkan alasan pembatalan", await BodyAsync(owner, order));

        await StepAsync(owner, order, "Cancel", new() { ["Reason"] = "MÃ¼ÅŸteri onarÄ±mdan vazgeÃ§ti" });
        var page = await BodyAsync(owner, order);
        Assert.Contains("stamp stamp-warn is-current", page);
        Assert.Contains("MÃ¼ÅŸteri onarÄ±mdan vazgeÃ§ti", page);
        Assert.DoesNotContain("Langkah berikutnya", page);
    }

    [DockerFact]
    public async Task Workshop_and_customer_notes_land_in_separate_lists()
    {
        var owner = await _flows.OwnerAsync("is-not@example.com");
        var order = await ReceiveAsync(owner, "0812 3456 7890");

        await owner.SubmitAsync(order, order + "/notes", new() { ["Text"] = "Anakart nemli", ["VisibleToCustomer"] = "false" });
        await owner.SubmitAsync(order, order + "/notes", new() { ["Text"] = "ParÃ§a PerÅŸembe gelecek", ["VisibleToCustomer"] = "true" });

        var page = await BodyAsync(owner, order);
        var customerList = page[page.IndexOf("note-list is-customer", StringComparison.Ordinal)..];
        var workshopList = page[page.IndexOf("class=\"note-list\"", StringComparison.Ordinal)..page.IndexOf("Catatan yang dilihat pelanggan", StringComparison.Ordinal)];
        Assert.Contains("Anakart nemli", workshopList);
        Assert.DoesNotContain("ParÃ§a PerÅŸembe gelecek", workshopList);
        Assert.Contains("ParÃ§a PerÅŸembe gelecek", customerList);
        Assert.DoesNotContain("Anakart nemli", customerList);
    }

    [DockerFact]
    public async Task Technician_can_move_the_order_forward()
    {
        var owner = await _flows.OwnerAsync("is-usta-sahip@example.com");
        var technician = await _flows.TechnicianAsync(owner, "is-usta@example.com");
        var order = await ReceiveAsync(technician, "0812 3456 7890");

        Assert.Equal(order, Browser.RedirectOf(await StepAsync(technician, order, "StartDiagnosis")));
        Assert.Contains("stamp is-current", await BodyAsync(technician, order));
    }

    [DockerFact]
    public async Task Another_shop_cannot_touch_the_order()
    {
        var owner = await _flows.OwnerAsync("is-kurban@example.com");
        var order = await ReceiveAsync(owner, "0812 3456 7890");
        var attacker = await _flows.OwnerAsync("is-saldirgan@example.com");

        var response = await attacker.SubmitAsync("/panel", order + "/steps", new() { ["Step"] = "Cancel", ["Reason"] = "Sabotaj" });
        var note = await attacker.SubmitAsync("/panel", order + "/notes", new() { ["Text"] = "SÄ±zma" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, note.StatusCode);
        Assert.Contains(">Mulai pengecekan<", await BodyAsync(owner, order));
        Assert.DoesNotContain("SÄ±zma", await BodyAsync(owner, order));
    }

    private static Task<HttpResponseMessage> StepAsync(Browser browser, string order, string step, Dictionary<string, string>? extra = null)
    {
        var fields = extra ?? [];
        fields["Step"] = step;
        return browser.SubmitAsync(order, order + "/steps", fields);
    }
}
