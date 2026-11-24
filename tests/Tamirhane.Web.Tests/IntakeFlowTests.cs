using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tamirhane.Domain.WorkOrders;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>Cihaz kabulÃ¼ HTTP Ã¼zerinden: telefonla arama, yeni ve kayÄ±tlÄ± mÃ¼ÅŸteri, doÄŸrulama, overposting, kiracÄ± ayrÄ±mÄ±.</summary>
[Collection(WebCollection.Name)]
public sealed partial class IntakeFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task New_customer_is_received_and_the_work_order_page_opens()
    {
        var owner = await _flows.OwnerAsync("kabul-yeni@example.com");

        var search = await BodyAsync(owner, "/work-orders/new?phone=0812 3456 7890");
        Assert.Contains("Tidak ada pelanggan terdaftar dengan", search);

        var response = await ReceiveAsync(owner, "0812 3456 7890", NewCustomer());

        var page = await BodyAsync(owner, Browser.RedirectOf(response));
        Assert.Matches(@"Order Servis \d{4}-0001 dibuka\.", page);
        Assert.Contains("Zeynep Arslan", page);
        Assert.Contains("0812 3456 7890", page);
        Assert.Contains("490154203237518", page);
        Assert.Contains("Diterima", page);
        Assert.Contains("Cetak tanda terima", page);
        Assert.Contains("Kode sandi perangkat tersimpan", page);
        // Åžifre personel iÃ§in kapalÄ± bir bÃ¶lÃ¼mde, tÄ±klayÄ±nca aÃ§Ä±lÄ±r (ekranda sÃ¼rekli durmaz).
        Assert.Matches(@"<details class=""passcode"">\s*<summary>Tampilkan kode sandi perangkat</summary>\s*<p><code>4826</code>", page);
    }

    [DockerFact]
    public async Task Returning_customer_is_found_by_phone_and_the_same_device_is_reused()
    {
        var owner = await _flows.OwnerAsync("kabul-tekrar@example.com");
        await ReceiveAsync(owner, "0812 3456 7890", NewCustomer());

        var search = await BodyAsync(owner, "/work-orders/new?phone=" + Uri.EscapeDataString("+62 812 3456 7890")); // farklÄ± yazÄ±m, aynÄ± numara
        Assert.Contains("<strong>Zeynep Arslan</strong>", search);
        Assert.DoesNotContain("Tidak ada pelanggan terdaftar dengan", search);
        var customerId = Hidden("Input.CustomerId").Match(search).Groups[1].Value;
        var deviceId = DeviceRadio().Match(search).Groups[1].Value;

        var second = await ReceiveAsync(owner, "+62 812 3456 7890", new()
        {
            ["Input.CustomerId"] = customerId,
            ["Input.DeviceId"] = deviceId,
            ["Input.Complaint"] = "Åžarj soketi gevÅŸek",
        });

        if (second.StatusCode != HttpStatusCode.Redirect)
            Assert.Fail(Regex.Match(await second.Content.ReadAsStringAsync(), @"notice-error[^>]*>[\s\S]{0,400}").Value);
        Assert.Matches(@"\d{4}-0002", await BodyAsync(owner, Browser.RedirectOf(second)));
        await AssertShopCountsAsync("kabul-tekrar@example.com", customers: 1, devices: 1, orders: 2);
    }

    [DockerFact]
    public async Task New_customer_without_consent_is_not_saved()
    {
        var owner = await _flows.OwnerAsync("kabul-pdpa@example.com");
        var fields = NewCustomer();
        fields.Remove("Input.KvkkConsent");

        var response = await ReceiveAsync(owner, "0812 3456 7890", fields);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Pelanggan baru tidak bisa disimpan sebelum pemberitahuan privasi dibacakan dan pelanggan memberikan persetujuan.", await response.Content.ReadAsStringAsync());
        await AssertShopCountsAsync("kabul-pdpa@example.com", customers: 0, devices: 0, orders: 0);
    }

    [DockerFact]
    public async Task Wrong_imei_and_missing_complaint_are_explained()
    {
        var owner = await _flows.OwnerAsync("kabul-hata@example.com");
        var fields = NewCustomer();
        fields["Input.Imei"] = "490154203237519"; // son hane tutmuyor

        var badImei = await (await ReceiveAsync(owner, "0812 3456 7890", fields)).Content.ReadAsStringAsync();
        Assert.Contains("IMEI harus 15 digit dengan digit cek yang valid. Tekan *#06# di ponsel untuk melihatnya; jika ragu, kosongkan.", badImei);
        Assert.Contains("value=\"Zeynep Arslan\"", badImei); // yazÄ±lanlar kaybolmaz

        fields["Input.Imei"] = "";
        fields["Input.Complaint"] = "";
        Assert.Contains("Masukkan keluhan pelanggan.", await (await ReceiveAsync(owner, "0812 3456 7890", fields)).Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task Invalid_phone_stays_on_the_search_step()
    {
        var owner = await _flows.OwnerAsync("kabul-tel@example.com");

        var page = await BodyAsync(owner, "/work-orders/new?phone=12345");

        Assert.Contains("Masukkan nomor telepon seperti 0812 3456 7890.", page);
        Assert.Contains("Cari pelanggan", page);
    }

    [DockerFact]
    public async Task Extra_form_fields_cannot_set_shop_branch_or_status()
    {
        var owner = await _flows.OwnerAsync("kabul-fazla@example.com");
        var foreignShop = Guid.NewGuid();
        var fields = NewCustomer();
        fields["Input.ShopId"] = foreignShop.ToString();
        fields["ShopId"] = foreignShop.ToString();
        fields["Input.BranchId"] = Guid.NewGuid().ToString();
        fields["Input.Status"] = nameof(WorkOrderStatus.Delivered);
        fields["Input.Number"] = "2026-9999";

        var response = await ReceiveAsync(owner, "0812 3456 7890", fields);
        var id = Guid.Parse(Browser.RedirectOf(response).Split('/')[^1]);

        await app.AsShopAsync(Guid.Empty, async db =>
        {
            var order = await db.WorkOrders.IgnoreQueryFilters().SingleAsync(o => o.Id == id);
            var staff = await db.Staff.IgnoreQueryFilters().SingleAsync(s => s.ShopId == order.ShopId);
            Assert.NotEqual(foreignShop, order.ShopId);
            Assert.Equal(staff.BranchId, order.BranchId);
            Assert.Equal(WorkOrderStatus.Received, order.Status);
            Assert.NotEqual("2026-9999", order.Number);
        });
    }

    [DockerFact]
    public async Task Another_shops_customer_and_work_order_are_out_of_reach()
    {
        var victim = await _flows.OwnerAsync("kabul-kurban@example.com");
        var victimOrder = Browser.RedirectOf(await ReceiveAsync(victim, "0812 3456 7890", NewCustomer()));
        var attacker = await _flows.OwnerAsync("kabul-saldirgan@example.com");

        Assert.Equal(HttpStatusCode.NotFound, (await attacker.GetAsync(victimOrder)).StatusCode);

        // KurbanÄ±n mÃ¼ÅŸteri kimliÄŸini bilse bile o mÃ¼ÅŸteriye iÅŸ emri aÃ§amaz.
        var victimOrderId = Guid.Parse(victimOrder.Split('/')[^1]);
        Guid victimCustomer = default;
        await app.AsShopAsync(Guid.Empty, async db => victimCustomer = await db.WorkOrders.IgnoreQueryFilters()
            .Where(o => o.Id == victimOrderId).Select(o => o.CustomerId).SingleAsync());
        var response = await ReceiveAsync(attacker, "0812 3456 7890", new()
        {
            ["Input.CustomerId"] = victimCustomer.ToString(),
            ["Input.DeviceKind"] = "Phone", ["Input.Brand"] = "Apple", ["Input.Model"] = "iPhone 13",
            ["Input.Complaint"] = "Ekran kÄ±rÄ±k",
        });
        Assert.Contains("Pelanggan yang dipilih tidak ditemukan. Cari lagi berdasarkan telepon.", await response.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task Signed_out_visitor_is_sent_to_login()
    {
        Assert.StartsWith("/account/login", Browser.RedirectOf(await app.NewBrowser().GetAsync("/work-orders/new")));
    }

    private static Dictionary<string, string> NewCustomer() => new()
    {
        ["Input.CustomerName"] = "Zeynep Arslan",
        ["Input.CustomerEmail"] = "",
        ["Input.KvkkConsent"] = "true",
        ["Input.DeviceKind"] = "Phone",
        ["Input.Brand"] = "Apple",
        ["Input.Model"] = "iPhone 13",
        ["Input.Imei"] = "490154203237518",
        ["Input.Complaint"] = "Ekran kÄ±rÄ±k",
        ["Input.Condition"] = "Kasada Ã§izik",
        ["Input.Accessories"] = "KÄ±lÄ±f",
        ["Input.Passcode"] = "4826",
    };

    /// <summary>Telefonla arama sayfasÄ±nÄ± aÃ§ar (CSRF token'Ä± oradan) ve kabul formunu gÃ¶nderir.</summary>
    private static Task<HttpResponseMessage> ReceiveAsync(Browser browser, string phone, Dictionary<string, string> fields)
    {
        fields["Input.Phone"] = phone;
        return browser.SubmitAsync($"/work-orders/new?phone={Uri.EscapeDataString(phone)}", "/work-orders/new", fields);
    }

    private async Task AssertShopCountsAsync(string ownerEmail, int customers, int devices, int orders)
    {
        await app.AsShopAsync(Guid.Empty, async db =>
        {
            var userId = await db.Users.Where(u => u.Email == ownerEmail).Select(u => u.Id).SingleAsync();
            var shopId = await db.Staff.IgnoreQueryFilters().Where(s => s.UserId == userId).Select(s => s.ShopId).SingleAsync();
            Assert.Equal(customers, await db.Customers.IgnoreQueryFilters().CountAsync(c => c.ShopId == shopId));
            Assert.Equal(devices, await db.Devices.IgnoreQueryFilters().CountAsync(d => d.ShopId == shopId));
            Assert.Equal(orders, await db.WorkOrders.IgnoreQueryFilters().CountAsync(o => o.ShopId == shopId));
        });
    }

    private static Regex Hidden(string name) => new($"name=\"{Regex.Escape(name)}\" value=\"([^\"]*)\"");

    [GeneratedRegex(@"name=""Input.DeviceId"" value=""([0-9a-f-]{36})""")]
    private static partial Regex DeviceRadio();
}
