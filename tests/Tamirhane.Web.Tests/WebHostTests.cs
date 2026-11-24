using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Customers;
using Tamirhane.Infrastructure.Persistence;

namespace Tamirhane.Web.Tests;

/// <summary>Uygulama gerÃ§ek servis kayÄ±tlarÄ±yla aÃ§Ä±lÄ±yor ve veritabanÄ±na doÄŸru baÄŸlanÄ±yor.</summary>
[Collection(WebCollection.Name)]
public sealed class WebHostTests(TamirhaneWebFactory app)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.FromHours(7));

    [DockerFact]
    public async Task Health_endpoint_reports_a_reachable_database()
    {
        var response = await app.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task Liveness_endpoint_answers_over_plain_http_without_the_database()
    {
        // Sunucunun iÃ§inden http ile sorulan saÄŸlÄ±k kontrolÃ¼ https'e yÃ¶nlendirilmemeli.
        var response = await app.CreateClient(new() { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false })
            .GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task Home_page_opens()
    {
        // Sayfada form (dil seÃ§ici) var; CSRF Ã§erezi Secure olduÄŸu iÃ§in https tarayÄ±cÄ±yla aÃ§Ä±lÄ±r, yayÄ±nda da her istek https.
        var response = await app.NewBrowser().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [DockerFact]
    public async Task Visitor_without_a_session_sees_no_shop_records()
    {
        var shopId = Guid.NewGuid();
        await app.AsShopAsync(shopId, async db =>
        {
            db.Customers.Add(Customer.Create(shopId, "AyÅŸe Kaya", "0812 3456 7890", null, true, Now).Value);
            await db.SaveChangesAsync();
        });

        // UygulamanÄ±n kendi kaydÄ±: oturum yokken atÃ¶lye boÅŸtur, filtre hiÃ§bir mÃ¼ÅŸteri dÃ¶ndÃ¼rmez.
        using var scope = app.Services.CreateScope();
        Assert.Equal(Guid.Empty, scope.ServiceProvider.GetRequiredService<ICurrentUser>().ShopId);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<TamirhaneDbContext>().Customers.ToListAsync());

        await app.AsShopAsync(shopId, async db => Assert.Single(await db.Customers.ToListAsync()));
    }
}
