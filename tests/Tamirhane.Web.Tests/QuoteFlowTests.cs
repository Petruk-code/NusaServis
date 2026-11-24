using System.Net;
using Microsoft.EntityFrameworkCore;
using Tamirhane.Domain.Inventory;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>Teklif dÃ¼zenleme HTTP Ã¼zerinden: stoktan parÃ§a, serbest kalem, sunucuda toplam, gÃ¶nderince kilit.</summary>
[Collection(WebCollection.Name)]
public sealed class QuoteFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Stock_part_and_labor_add_up_on_the_server()
    {
        var owner = await _flows.OwnerAsync("teklif-toplam@example.com");
        await AddStockPartAsync("teklif-toplam@example.com", quantity: 3);
        var order = await DiagnosingAsync(owner, "0812 3456 7890");
        var page = await BodyAsync(owner, order);
        var partId = System.Text.RegularExpressions.Regex.Match(page, "<option value=\"([0-9a-f-]{36})\">iPhone 13 ekran paneli").Groups[1].Value;
        Assert.Contains("stok 3", page);

        await AddLineAsync(owner, order, new() { ["Kind"] = "Part", ["PartId"] = partId, ["Quantity"] = "1" }); // fiyat ve KDV boÅŸ
        await AddLineAsync(owner, order, new() { ["Kind"] = "Labor", ["Description"] = "Ekran deÄŸiÅŸimi iÅŸÃ§iliÄŸi", ["Quantity"] = "1", ["UnitPrice"] = "500", ["DiscountRate"] = "10" });

        page = await BodyAsync(owner, order);
        Assert.Contains("iPhone 13 ekran paneli", page);
        Assert.Contains("Ekran deÄŸiÅŸimi iÅŸÃ§iliÄŸi", page);
        // 2500 + 450 = 2950 net, KDV %20 = 590, toplam 3540
        Assert.Contains("Rp 3.540", page);
        Assert.Contains("Rp 590", page);
        Assert.Contains(">Kirim estimasi biaya ke pelanggan<", page); // kalem varsa sÄ±radaki adÄ±m teklif
    }

    [DockerFact]
    public async Task Sending_the_quote_locks_it()
    {
        var owner = await _flows.OwnerAsync("teklif-kilit@example.com");
        var order = await DiagnosingAsync(owner, "0812 3456 7890");
        await AddLineAsync(owner, order, new() { ["Kind"] = "Labor", ["Description"] = "Batarya deÄŸiÅŸimi", ["Quantity"] = "1", ["UnitPrice"] = "800" });

        await owner.SubmitAsync(order, order + "/steps", new() { ["Step"] = "SendQuote" });

        var page = await BodyAsync(owner, order);
        Assert.Contains("stamp is-current", page);
        Assert.Contains("Menunggu persetujuan", page);
        Assert.Contains("Estimasi biaya dikunci saat menunggu persetujuan pelanggan.", page);
        Assert.DoesNotContain("Tambah item", page);
        Assert.DoesNotContain("/remove\"", page);

        await AddLineAsync(owner, order, new() { ["Kind"] = "Labor", ["Description"] = "Gizli ek", ["Quantity"] = "1", ["UnitPrice"] = "1" });
        page = await BodyAsync(owner, order);
        Assert.Contains("Estimasi biaya tidak bisa diubah setelah pelanggan mengambil keputusan.", page);
        Assert.DoesNotContain("Gizli ek", page);
    }

    [DockerFact]
    public async Task Free_line_without_price_is_explained_and_removed_line_drops_from_totals()
    {
        var owner = await _flows.OwnerAsync("teklif-hata@example.com");
        var order = await DiagnosingAsync(owner, "0812 3456 7890");

        await AddLineAsync(owner, order, new() { ["Kind"] = "Labor", ["Description"] = "Temizlik", ["Quantity"] = "1" });
        Assert.Contains("Masukkan harga satuan: nol atau lebih, maksimal dua desimal.", await BodyAsync(owner, order));

        await AddLineAsync(owner, order, new() { ["Kind"] = "Labor", ["Description"] = "Temizlik", ["Quantity"] = "1", ["UnitPrice"] = "250,50" });
        var page = await BodyAsync(owner, order);
        Assert.Contains("Rp 301", page); // 250,50 + %20
        var remove = System.Text.RegularExpressions.Regex.Match(page, "action=\"(/work-orders/[0-9a-f-]{36}/quote-lines/[0-9a-f-]{36}/remove)\"").Groups[1].Value;

        await owner.SubmitAsync(order, remove, new());
        page = await BodyAsync(owner, order);
        Assert.DoesNotContain("Temizlik", page);
        Assert.Contains(">Mulai perbaikan<", page);
    }

    [DockerFact]
    public async Task Another_shop_cannot_add_lines()
    {
        var owner = await _flows.OwnerAsync("teklif-kurban@example.com");
        var order = await DiagnosingAsync(owner, "0812 3456 7890");
        var attacker = await _flows.OwnerAsync("teklif-saldirgan@example.com");

        var response = await attacker.SubmitAsync("/panel", order + "/quote-lines",
            new() { ["Kind"] = "Labor", ["Description"] = "SÄ±zma", ["Quantity"] = "1", ["UnitPrice"] = "1" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("SÄ±zma", await BodyAsync(owner, order));
    }

    private static async Task<string> DiagnosingAsync(Browser owner, string phone)
    {
        var order = await ReceiveAsync(owner, phone);
        await owner.SubmitAsync(order, order + "/steps", new() { ["Step"] = "StartDiagnosis" });
        return order;
    }

    private static Task<HttpResponseMessage> AddLineAsync(Browser browser, string order, Dictionary<string, string> fields) =>
        browser.SubmitAsync(order, order + "/quote-lines", fields);

    /// <summary>Stok ekranÄ± #24'te; ÅŸimdilik parÃ§ayÄ± ve stoÄŸunu doÄŸrudan veritabanÄ±na koyar.</summary>
    private async Task AddStockPartAsync(string ownerEmail, int quantity)
    {
        Guid shopId = default, branchId = default;
        await app.AsShopAsync(Guid.Empty, async db =>
        {
            var userId = await db.Users.Where(u => u.Email == ownerEmail).Select(u => u.Id).SingleAsync();
            var staff = await db.Staff.IgnoreQueryFilters().SingleAsync(s => s.UserId == userId);
            (shopId, branchId) = (staff.ShopId, staff.BranchId);
        });
        await app.AsShopAsync(shopId, async db =>
        {
            var part = Part.Create(shopId, "IP13-SCR", "iPhone 13 ekran paneli", 1800m, 2500m, 1).Value;
            var level = StockLevel.Create(part.Id, branchId);
            var movement = level.Receive(quantity, Guid.NewGuid(), DateTimeOffset.UtcNow, null).Value;
            db.Parts.Add(part);
            db.StockLevels.Add(level);
            db.StockMovements.Add(movement);
            await db.SaveChangesAsync();
        });
    }
}
