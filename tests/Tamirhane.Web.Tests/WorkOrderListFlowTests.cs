using System.Net;
using System.Text.RegularExpressions;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>Ã„Â°Ã…Å¸ emri listesi ve hÃ„Â±zlÃ„Â± arama: sÃƒÂ¼zgeÃƒÂ§ler, arama tÃƒÂ¼rleri, tek sonuÃƒÂ§ta yÃƒÂ¶nlendirme ve kiracÃ„Â± ayrÃ„Â±mÃ„Â±; HTTP ÃƒÂ¼zerinden.</summary>
[Collection(WebCollection.Name)]
public sealed partial class WorkOrderListFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Open_list_shows_number_device_complaint_customer_status_and_waiting_time()
    {
        var owner = await _flows.OwnerAsync("liste@example.com");
        var order = await ReceiveAsync(owner, "Selin YÃ„Â±lmaz", "0812 3456 7890", "Samsung", "Galaxy S23", "Ã…Å¾arj olmuyor");

        var list = await BodyAsync(owner, "/work-orders");

        foreach (var expected in new[] { $"href=\"{order}\"", "Samsung Galaxy S23", "Ã…Å¾arj olmuyor", "Selin YÃ„Â±lmaz", "Diterima", "Hari ini" })
            Assert.Contains(expected, list);
        Assert.Matches(@"<a aria-current=""page"" href=""/work-orders"">", list); // menÃƒÂ¼de Ã„Â°Ã…Å¸ emirleri seÃƒÂ§ili
    }

    [DockerFact]
    public async Task Search_by_name_phone_imei_or_number_with_one_result_opens_the_work_order()
    {
        var owner = await _flows.OwnerAsync("arama@example.com");
        var order = await ReceiveAsync(owner, "Kemal Ãƒâ€“ztÃƒÂ¼rk", "0812 3456 7890", "Apple", "iPhone 12", "HoparlÃƒÂ¶r cÃ„Â±zÃ„Â±rtÃ„Â±lÃ„Â±", imei: "490154203237518");
        await ReceiveAsync(owner, "Derya KoÃƒÂ§", "0533 303 03 03", "Xiaomi", "Redmi Note 12", "Kamera bulanÃ„Â±k");
        var number = NumberPattern().Match(await BodyAsync(owner, order)).Value;

        foreach (var query in new[] { "kemal", "Ãƒâ€“ztÃƒÂ¼rk", "0812 3456 7890", "081234567890", "49015 4203 237518", number, number[5..].TrimStart('0'), "iphone 12" })
            Assert.Equal(order, Browser.RedirectOf(await owner.GetAsync("/work-orders?q=" + Uri.EscapeDataString(query))));
    }

    [DockerFact]
    public async Task Search_with_several_results_lists_them_and_finds_closed_work_orders_too()
    {
        var owner = await _flows.OwnerAsync("coklu@example.com");
        var first = await ReceiveAsync(owner, "Ahmet Demir", "0812 3456 7890", "Apple", "iPhone 11", "Pil Ã…Å¸iÃ…Å¸ti");
        var second = await ReceiveAsync(owner, "Ahmet Kurt", "0812 3456 7891", "Apple", "iPad Air", "Ekran kÃ„Â±rÃ„Â±k");
        await StepAsync(owner, first, new() { ["Step"] = "Cancel", ["Reason"] = "MÃƒÂ¼Ã…Å¸teri vazgeÃƒÂ§ti" });

        var found = await owner.GetAsync("/work-orders?q=ahmet");
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        var body = await found.Content.ReadAsStringAsync();
        Assert.Contains($"href=\"{first}\"", body);
        Assert.Contains($"href=\"{second}\"", body);
        Assert.Contains("Dibatalkan", body);

        // AÃƒÂ§Ã„Â±k sÃƒÂ¼zgeÃƒÂ§te iptal edilen iÃ…Å¸ gÃƒÂ¶rÃƒÂ¼nmez, kapananlarda gÃƒÂ¶rÃƒÂ¼nÃƒÂ¼r.
        var open = await BodyAsync(owner, "/work-orders");
        Assert.DoesNotContain($"href=\"{first}\"", open);
        Assert.Contains($"href=\"{second}\"", open);
        var closed = await BodyAsync(owner, "/work-orders?filter=Closed");
        Assert.Contains($"href=\"{first}\"", closed);
        Assert.DoesNotContain($"href=\"{second}\"", closed);
    }

    [DockerFact]
    public async Task Status_filters_pick_the_matching_work_orders()
    {
        var owner = await _flows.OwnerAsync("suzgec@example.com");
        var waiting = await ReceiveAsync(owner, "Ece Ã…Å¾ahin", "0812 3456 7890", "Huawei", "P30", "AÃƒÂ§Ã„Â±lmÃ„Â±yor");
        var diagnosing = await ReceiveAsync(owner, "Ece TunÃƒÂ§", "0812 3456 7892", "Huawei", "MatePad", "Dokunmatik yok");
        await StepAsync(owner, diagnosing, new() { ["Step"] = "StartDiagnosis" });

        var inRepair = await BodyAsync(owner, "/work-orders?filter=InRepair");
        Assert.DoesNotContain($"href=\"{waiting}\"", inRepair);
        Assert.DoesNotContain($"href=\"{diagnosing}\"", inRepair);
        Assert.Contains("Tidak ada order servis di filter ini", inRepair);

        var overdue = await BodyAsync(owner, "/work-orders?filter=Overdue");
        Assert.DoesNotContain($"href=\"{waiting}\"", overdue); // bugÃƒÂ¼n aÃƒÂ§Ã„Â±ldÃ„Â±, 30 gÃƒÂ¼nÃƒÂ¼ geÃƒÂ§medi

        var all = await BodyAsync(owner, "/work-orders?filter=All");
        Assert.Contains($"href=\"{waiting}\"", all);
        Assert.Contains($"href=\"{diagnosing}\"", all);
        Assert.Contains("Pengecekan / Diagnosa", all);
    }

    [DockerFact]
    public async Task Empty_states_say_what_to_do()
    {
        var owner = await _flows.OwnerAsync("bos-liste@example.com");

        Assert.Contains("Tidak ada order terbuka", await BodyAsync(owner, "/work-orders"));
        var missing = await BodyAsync(owner, "/work-orders?q=" + Uri.EscapeDataString("<b>Yok</b>"));
        Assert.Contains("Tidak ada order servis untuk", missing);
        Assert.Contains("&lt;b&gt;Yok&lt;/b&gt;", missing); // aranan metin kaÃƒÂ§Ã„Â±Ã…Å¸lanÃ„Â±r
        Assert.DoesNotContain("<b>Yok</b>", missing);
    }

    [DockerFact]
    public async Task Other_shops_work_orders_never_show_up_and_list_needs_sign_in()
    {
        var owner = await _flows.OwnerAsync("kiraci-liste@example.com");
        var order = await ReceiveAsync(owner, "Burak AydÃ„Â±n", "0812 3456 7890", "OnePlus", "Nord 3", "Wi-Fi ÃƒÂ§ekmiyor");
        var number = NumberPattern().Match(await BodyAsync(owner, order)).Value;

        var attacker = await _flows.OwnerAsync("kiraci-saldirgan@example.com");
        foreach (var query in new[] { "", "?filter=All", "?q=Burak", "?q=" + Uri.EscapeDataString("0812 3456 7890"), "?q=" + number })
        {
            var response = await attacker.GetAsync("/work-orders" + query);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain(order, await response.Content.ReadAsStringAsync());
        }

        Assert.StartsWith("/account/login", Browser.RedirectOf(await app.NewBrowser().GetAsync("/work-orders")));
    }

    private static async Task<string> ReceiveAsync(Browser browser, string name, string phone, string brand, string model,
        string complaint, string? imei = null)
    {
        var response = await browser.SubmitAsync($"/work-orders/new?phone={Uri.EscapeDataString(phone)}", "/work-orders/new", new()
        {
            ["Input.Phone"] = phone, ["Input.CustomerName"] = name, ["Input.KvkkConsent"] = "true",
            ["Input.DeviceKind"] = "Phone", ["Input.Brand"] = brand, ["Input.Model"] = model,
            ["Input.Imei"] = imei ?? "", ["Input.Complaint"] = complaint,
        });
        return Browser.RedirectOf(response);
    }

    private static async Task StepAsync(Browser browser, string order, Dictionary<string, string> fields)
    {
        var response = await browser.SubmitAsync(order, order + "/steps", fields);
        Assert.Equal(order, Browser.RedirectOf(response));
    }

    [GeneratedRegex(@"\d{4}-\d{4}")]
    private static partial Regex NumberPattern();
}
