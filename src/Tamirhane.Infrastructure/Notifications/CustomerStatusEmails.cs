using Microsoft.EntityFrameworkCore;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Common;
using Tamirhane.Domain.WorkOrders;
using Tamirhane.Infrastructure.Background;
using Tamirhane.Infrastructure.Email;
using Tamirhane.Infrastructure.Persistence;

namespace Tamirhane.Infrastructure.Notifications;

/// <summary>
/// Müşteriye durum e-postası (#30): iş emrinin durumu değişince, müşterinin e-postası varsa takip linkiyle bir e-posta.
/// Bu iş sadece e-postayı hazırlayıp kuyruğa koyar; gönderim ve tekrar deneme ayrı bir iştir. Gönderim başarısız
/// olsa da iş emri işlemi etkilenmez: işlem kaydedildikten sonra kuyruğa girmiştir.
/// Arka planda oturum olmadan çalışır: kiracı filtresi kapalıdır, her sorgu atölye kimliğiyle süzülür.
/// Demo atölyede gönderilmez: ziyaretçi müşteri olarak başkasının adresini yazabilir.
/// </summary>
internal sealed class CustomerStatusEmails(TamirhaneDbContext db, EmailOutbox outbox, ITrackingLinks links)
    : IWorkOrderNotificationHandler
{
    public async Task HandleAsync(WorkOrderNotification notification, CancellationToken ct)
    {
        var order = await db.WorkOrders.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == notification.WorkOrderId && o.ShopId == notification.ShopId, ct);
        // İş kuyrukta beklerken durum yine değiştiyse eski haberi gönderme: yeni durumun e-postası zaten sırada.
        if (order is null || order.Status != notification.Status)
            return;

        var email = await db.Customers.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Id == order.CustomerId && c.ShopId == order.ShopId)
            .Select(c => c.Email).SingleOrDefaultAsync(ct);
        if (email is null)
            return;

        var shop = await db.Shops.IgnoreQueryFilters().AsNoTracking().SingleAsync(s => s.Id == order.ShopId, ct);
        if (shop.IsDemo)
            return;
        var branch = await db.Branches.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.Id == order.BranchId && b.ShopId == order.ShopId)
            .Select(b => new { b.Address, b.Phone }).SingleOrDefaultAsync(ct);
        var device = await db.Devices.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.Id == order.DeviceId && d.ShopId == order.ShopId)
            .Select(d => d.Brand + " " + d.Model).SingleAsync(ct);

        var message = Build(email, shop.Locale, shop.Name, order.Number, device, order.Status,
            branch?.Address, branch?.Phone, links.For(order.TrackingCode));
        outbox.Enqueue(message, $"müşteri durumu {order.Status}");
    }

    /// <summary>Email status pelanggan. Bahasa mengikuti locale toko (id/en).</summary>
    internal static EmailMessage Build(
        string to, string locale, string shopName, string number, string device, WorkOrderStatus status,
        string? address, string? phone, Uri trackingLink)
    {
        var en = locale == "en";
        var lang = en ? "en" : "id";
        var (headline, lead) = StatusTexts.For(status, lang);
        var paragraphs = new List<string> { lead };
        if ((status is WorkOrderStatus.Ready or WorkOrderStatus.Declined) && (address ?? phone) is not null)
            paragraphs.Add(en
                ? $"Pick-up address: {Join(address, phone)}"
                : $"Alamat pengambilan: {Join(address, phone)}");
        paragraphs.Add(en ? $"Work order: {number} ({device})" : $"Order servis: {number} ({device})");

        return EmailLayout.Build(to, lang, $"{device}: {headline}", paragraphs,
            new EmailAction(en ? "Track your repair" : "Lacak servis Anda", trackingLink),
            en
                ? $"You get this email because your email address is on your service record at {shopName}. The link is personal; please do not share it."
                : $"Anda menerima email ini karena alamat email Anda tercatat pada data servis di {shopName}. Tautan ini bersifat pribadi; mohon jangan dibagikan.",
            fromName: shopName);
    }

    private static string Join(string? address, string? phone) =>
        string.Join(" · ", new[] { address, phone is null ? null : DisplayPhone(phone) }.Where(p => p is not null));

    /// <summary>Format nomor untuk ditampilkan di email.</summary>
    private static string DisplayPhone(string e164)
    {
        if (e164.StartsWith("+62"))
        {
            var digits = "0" + e164[3..];
            return digits.Length >= 11 ? $"{digits[..4]} {digits[4..8]} {digits[8..]}" : digits;
        }
        return e164.Length == 13 && e164.StartsWith("+90")
            ? $"0{e164[3..6]} {e164[6..9]} {e164[9..11]} {e164[11..13]}"
            : e164;
    }
}
