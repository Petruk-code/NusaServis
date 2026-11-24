using System.Globalization;
using System.Net;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>SÃƒÆ’Ã‚Â¶z verilen teslim gÃƒÆ’Ã‚Â¼nÃƒÆ’Ã‚Â¼: kabulde verilir, iÃƒâ€¦Ã…Â¸ emri sayfasÃƒâ€žÃ‚Â±nda deÃƒâ€žÃ…Â¸iÃƒâ€¦Ã…Â¸ir ya da kaldÃƒâ€žÃ‚Â±rÃƒâ€žÃ‚Â±lÃƒâ€žÃ‚Â±r; HTTP ÃƒÆ’Ã‚Â¼zerinden.</summary>
[Collection(WebCollection.Name)]
public sealed class PromiseFlowTests(TamirhaneWebFactory app)
{
    private static readonly CultureInfo Id = new("id-ID");
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));

    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Promise_given_at_intake_shows_on_the_work_order()
    {
        var owner = await _flows.OwnerAsync("soz-kabul@example.com");

        var response = await ReceiveAsync(owner, "0812 3456 7890", Today.AddDays(3).ToString("yyyy-MM-dd"));

        var page = await BodyAsync(owner, Browser.RedirectOf(response));
        Assert.Contains($"<dt>Tanggal pengambilan</dt><dd>{Today.AddDays(3).ToString("dddd, d MMMM", Id)}</dd>", page);
        Assert.Contains($"value=\"{Today.AddDays(3):yyyy-MM-dd}\"", page);
    }

    [DockerFact]
    public async Task Past_promise_at_intake_is_explained_and_nothing_is_saved()
    {
        var owner = await _flows.OwnerAsync("soz-gecmis@example.com");

        var response = await ReceiveAsync(owner, "0812 3456 7890", Today.AddDays(-1).ToString("yyyy-MM-dd"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Tanggal pengambilan harus hari ini atau dalam", await response.Content.ReadAsStringAsync());
        Assert.Contains("Tidak ada order terbuka", await BodyAsync(owner, "/work-orders"));
    }

    [DockerFact]
    public async Task Promise_is_changed_and_removed_on_the_work_order_page()
    {
        var owner = await _flows.OwnerAsync("soz-degis@example.com");
        var order = Browser.RedirectOf(await ReceiveAsync(owner, "0812 3456 7890", ""));
        Assert.DoesNotContain("<dt>Tanggal pengambilan</dt>", await BodyAsync(owner, order));

        await owner.SubmitAsync(order, order + "/promise", new() { ["Date"] = Today.AddDays(5).ToString("yyyy-MM-dd") });
        var page = await BodyAsync(owner, order);
        Assert.Contains("Tanggal janji disimpan", page);
        Assert.Contains(Today.AddDays(5).ToString("dddd, d MMMM", Id), page);

        await owner.SubmitAsync(order, order + "/promise", new() { ["Date"] = Today.AddDays(400).ToString("yyyy-MM-dd") });
        page = await BodyAsync(owner, order);
        Assert.Contains("Tanggal pengambilan harus hari ini atau dalam", page);
        Assert.Contains(Today.AddDays(5).ToString("dddd, d MMMM", Id), page);

        await owner.SubmitAsync(order, order + "/promise", new());
        page = await BodyAsync(owner, order);
        Assert.Contains("Tanggal pengambilan dihapus.", page);
        Assert.DoesNotContain("<dt>Tanggal pengambilan</dt>", page);
    }

    [DockerFact]
    public async Task Other_shops_cannot_change_the_promise()
    {
        var owner = await _flows.OwnerAsync("soz-sahip@example.com");
        var order = Browser.RedirectOf(await ReceiveAsync(owner, "0812 3456 7890", ""));
        var attacker = await _flows.OwnerAsync("soz-saldirgan@example.com");

        var response = await attacker.SubmitAsync("/panel", order + "/promise", new() { ["Date"] = Today.AddDays(1).ToString("yyyy-MM-dd") });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("<dt>Tanggal pengambilan</dt>", await BodyAsync(owner, order));
    }

    private static Task<HttpResponseMessage> ReceiveAsync(Browser browser, string phone, string promisedDate) =>
        browser.SubmitAsync($"/work-orders/new?phone={Uri.EscapeDataString(phone)}", "/work-orders/new", new()
        {
            ["Input.Phone"] = phone, ["Input.CustomerName"] = "Zeynep Arslan", ["Input.KvkkConsent"] = "true",
            ["Input.DeviceKind"] = "Phone", ["Input.Brand"] = "Apple", ["Input.Model"] = "iPhone 13",
            ["Input.Complaint"] = "Ekran kÃƒâ€žÃ‚Â±rÃƒâ€žÃ‚Â±k", ["Input.PromisedDate"] = promisedDate,
        });
}
