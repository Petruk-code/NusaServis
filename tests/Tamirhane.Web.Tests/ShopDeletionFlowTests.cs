using Microsoft.EntityFrameworkCore;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>Sahip atÃ¶lyesini Ayarlar'dan siler: ÅŸifre yeniden sorulur, bÃ¼tÃ¼n veri ve hesaplar gider, baÅŸka atÃ¶lye etkilenmez.</summary>
[Collection(WebCollection.Name)]
public sealed class ShopDeletionFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Wrong_password_keeps_the_shop()
    {
        var owner = await _flows.OwnerAsync("sil-yanlis@example.com");
        await ReceiveAsync(owner, "0812 3456 7890");

        var response = await owner.SubmitAsync("/settings", "/settings/delete", new() { ["password"] = "yanlis-sifre-1" });

        Assert.Equal("/settings", Browser.RedirectOf(response));
        Assert.Contains("Kata sandi salah.", await (await owner.GetAsync("/settings")).Content.ReadAsStringAsync());
        Assert.Equal((1, 1), await CountsAsync("sil-yanlis@example.com"));
    }

    [DockerFact]
    public async Task Owner_deletes_the_shop_with_all_its_data_and_accounts()
    {
        var owner = await _flows.OwnerAsync("sil-sahip@example.com");
        var technician = await _flows.TechnicianAsync(owner, "sil-usta@example.com");
        await ReceiveAsync(owner, "0812 3456 7890");
        var shopId = await ShopIdAsync("sil-sahip@example.com");
        var other = await _flows.OwnerAsync("sil-komsu@example.com");
        await ReceiveAsync(other, "0812 3456 7890");

        var response = await owner.SubmitAsync("/settings", "/settings/delete", new() { ["password"] = OwnerPassword });

        Assert.Equal("/", Browser.RedirectOf(response));
        Assert.Contains("Bengkel telah dihapus.", await (await owner.GetAsync("/")).Content.ReadAsStringAsync());
        Assert.StartsWith("/account/login", Browser.RedirectOf(await owner.GetAsync("/panel")));
        Assert.StartsWith("/account/login", Browser.RedirectOf(await technician.GetAsync("/panel")));
        var login = await app.NewBrowser().SubmitAsync("/account/login", "/account/login",
            new() { ["Email"] = "sil-sahip@example.com", ["Password"] = OwnerPassword });
        Assert.Contains("Email atau kata sandi salah.", await login.Content.ReadAsStringAsync());

        await app.AsShopAsync(shopId, async db =>
        {
            Assert.False(await db.Shops.IgnoreQueryFilters().AnyAsync(s => s.Id == shopId));
            Assert.False(await db.Customers.IgnoreQueryFilters().AnyAsync(c => c.ShopId == shopId));
            Assert.False(await db.WorkOrders.IgnoreQueryFilters().AnyAsync(o => o.ShopId == shopId));
            Assert.False(await db.Users.AnyAsync(u => u.Email == "sil-usta@example.com"));
        });
        Assert.Equal((1, 1), await CountsAsync("sil-komsu@example.com")); // komÅŸu atÃ¶lye yerinde
    }

    [DockerFact]
    public async Task Technician_cannot_delete_the_shop()
    {
        var owner = await _flows.OwnerAsync("sil-yetki@example.com");
        var technician = await _flows.TechnicianAsync(owner, "sil-yetki-usta@example.com");

        // Ayarlar sayfasÄ± teknisyene kapalÄ±; token baÅŸka sayfadan alÄ±nÄ±r, kural sunucuda.
        var response = await technician.SubmitAsync("/panel", "/settings/delete", new() { ["password"] = TechnicianPassword });

        Assert.NotEqual("/", response.Headers.Location?.OriginalString);
        Assert.Equal(1, (await CountsAsync("sil-yetki@example.com")).Shops);
    }

    private async Task<Guid> ShopIdAsync(string ownerEmail)
    {
        var shopId = Guid.Empty;
        await app.AsShopAsync(Guid.Empty, async db =>
        {
            var userId = await db.Users.Where(u => u.Email == ownerEmail).Select(u => u.Id).SingleAsync();
            shopId = await db.Staff.IgnoreQueryFilters().Where(s => s.UserId == userId).Select(s => s.ShopId).SingleAsync();
        });
        return shopId;
    }

    private async Task<(int Shops, int WorkOrders)> CountsAsync(string ownerEmail)
    {
        var shopId = await ShopIdAsync(ownerEmail);
        var counts = (0, 0);
        await app.AsShopAsync(shopId, async db =>
            counts = (await db.Shops.CountAsync(), await db.WorkOrders.CountAsync()));
        return counts;
    }
}
