using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.Shops;
using Tamirhane.Domain.WorkOrders;
using Tamirhane.Infrastructure.Notifications;

namespace Tamirhane.Infrastructure.Tests.Notifications;

/// <summary>Müşteriye durum e-postası: metinler (Docker gerekmez) ve kuyruktan gönderime kadar (gerçek PostgreSQL).</summary>
public sealed class CustomerStatusEmailTests
{
    private static readonly Uri Link = new("https://tamirhane.test/t/ABCD-EFGH");

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void Every_status_has_an_indonesian_and_an_english_email(WorkOrderStatus status)
    {
        foreach (var locale in new[] { "id", "en" })
        {
            var mail = CustomerStatusEmails.Build("budi@example.com", locale, "Bengkel Budi", "2026-0001", "Apple iPhone 13",
                status, "Jl. Melati 5", "+6281234567890", Link);

            Assert.StartsWith("Apple iPhone 13: ", mail.Subject);
            Assert.Contains(Link.AbsoluteUri, mail.Text);
            Assert.Contains("2026-0001", mail.Text);
            Assert.Equal("Bengkel Budi", mail.FromName);
            Assert.Contains($"lang=\"{locale}\"", mail.Html);
        }
    }

    public static TheoryData<WorkOrderStatus> AllStatuses() => new(Enum.GetValues<WorkOrderStatus>());

    [Fact]
    public void Ready_email_has_the_pick_up_address_and_phone()
    {
        var mail = CustomerStatusEmails.Build("a@example.com", "id", "Bengkel Budi", "2026-0001", "Apple iPhone 13",
            WorkOrderStatus.Ready, "Jl. Melati 5", "+6281234567890", Link);

        Assert.Equal("Apple iPhone 13: siap diambil", mail.Subject);
        Assert.Contains("Alamat pengambilan: Jl. Melati 5 · 0812 3456 7890", mail.Text);
    }

    [Fact]
    public void Other_statuses_do_not_repeat_the_address()
    {
        var mail = CustomerStatusEmails.Build("a@example.com", "id", "Bengkel Budi", "2026-0001", "Apple iPhone 13",
            WorkOrderStatus.InRepair, "Jl. Melati 5", "+6281234567890", Link);

        Assert.DoesNotContain("Jl. Melati", mail.Text);
    }

    [Fact]
    public void Missing_branch_details_are_left_out()
    {
        var mail = CustomerStatusEmails.Build("a@example.com", "en", "Kaya Telefon", "2026-0001", "Apple iPhone 13",
            WorkOrderStatus.Ready, null, null, Link);

        Assert.DoesNotContain("Pick-up address", mail.Text);
        Assert.Equal("Apple iPhone 13: ready for pick-up", mail.Subject);
    }
}

[Collection(PostgresCollection.Name)]
public sealed class CustomerStatusEmailDeliveryTests(PostgresFixture db)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 10, 0, 0, TimeSpan.FromHours(7));

    [DockerFact]
    public async Task Customer_with_an_email_gets_the_status_with_the_tracking_link()
    {
        var (shopId, order) = await SeedAsync("durum1@example.com");

        Notify(shopId, order, WorkOrderStatus.Received);

        var mail = Assert.Single(db.Emails.SentTo("durum1@example.com"));
        Assert.Equal("Apple iPhone 13: unit diterima", mail.Subject);
        Assert.Contains($"/t/{order.TrackingCode}", mail.Text);
        Assert.Equal("Bengkel Budi", mail.FromName);
    }

    [DockerFact]
    public async Task Customer_without_an_email_gets_nothing()
    {
        var (shopId, order) = await SeedAsync(null);
        var before = db.Emails.Count;

        Notify(shopId, order, WorkOrderStatus.Received);

        Assert.Equal(before, db.Emails.Count); // kuyruk boşalınca bile hiçbir adrese e-posta gitmemiş
    }

    [DockerFact]
    public async Task Demo_shop_never_emails_its_customers()
    {
        var (shopId, order) = await SeedAsync("durum-demo@example.com", demo: true);
        var before = db.Emails.Count;

        Notify(shopId, order, WorkOrderStatus.Received);

        Assert.Empty(db.Emails.SentTo("durum-demo@example.com"));
        Assert.Equal(before, db.Emails.Count);
    }

    [DockerFact]
    public async Task An_outdated_status_is_not_sent()
    {
        var (shopId, order) = await SeedAsync("durum3@example.com");
        var before = db.Emails.Count;

        // Kuyruktaki iş çalışmadan önce iş emri ilerledi: eski "teslim alındı" haberi gönderilmez
        Notify(shopId, order, WorkOrderStatus.Diagnosing);

        Assert.Empty(db.Emails.SentTo("durum3@example.com"));
        Assert.Equal(before, db.Emails.Count);
    }

    [DockerFact]
    public async Task Ids_from_another_shop_send_nothing()
    {
        var (_, order) = await SeedAsync("durum4@example.com");

        Notify(Guid.NewGuid(), order, WorkOrderStatus.Received);

        Assert.Empty(db.Emails.SentTo("durum4@example.com"));
    }

    private void Notify(Guid shopId, WorkOrder order, WorkOrderStatus status)
    {
        using var scope = db.AsShop(shopId);
        scope.Get<INotificationQueue>().Enqueue(new WorkOrderNotification(shopId, order.Id, status));
    }

    private async Task<(Guid ShopId, WorkOrder Order)> SeedAsync(string? email, bool demo = false)
    {
        var shop = (demo ? Shop.CreateDemo("Bengkel Budi", "id", Now) : Shop.Create("Bengkel Budi", "id", Now)).Value;
        var branch = Branch.Create(shop.Id, "Cabang Utama", "Jl. Melati 5", Now, "0212 5550 1234").Value;
        var phone = $"0812 {Random.Shared.Next(1000, 9999)} {Random.Shared.Next(1000, 9999)}";
        var customer = Customer.Create(shop.Id, "Budi Santoso", phone, email, true, Now).Value;
        var device = Device.Create(customer, DeviceKind.Phone, "Apple", "iPhone 13", null, null).Value;
        using (var anonymous = db.AsShop(Guid.Empty))
        {
            anonymous.Db.AddRange(shop, branch, customer, device);
            await anonymous.Db.SaveChangesAsync();
        }

        using var own = db.AsShop(shop.Id);
        var order = WorkOrder.Receive(shop.Id, branch.Id, "2026-0001", TrackingCode.Generate(), customer.Id, device.Id,
            "Ekran kırık", null, null, null, own.User.StaffId, Now).Value;
        own.Orders.Add(order);
        await own.UnitOfWork.SaveChangesAsync(default);
        return (shop.Id, order);
    }
}
