using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Shops;
using Tamirhane.Domain.WorkOrders;
using Tamirhane.Infrastructure.Background;
using Tamirhane.Infrastructure.Email;
using Tamirhane.Infrastructure.Identity;
using Tamirhane.Infrastructure.Persistence;


namespace Tamirhane.Infrastructure.Notifications;

/// <summary>
/// Atölyeye bildirim: istek sadece işi kuyruğa koyar. İş alıcıları bulur ve her alıcıya ayrı bir e-posta işi ekler;
/// böylece bir adrese gönderim yeniden denenirken diğerlerine ikinci kez gitmez.
/// </summary>
internal sealed class WorkshopNotifier(BackgroundJobQueue queue) : IWorkshopNotifier
{
    public void QuoteDecided(Guid shopId, Guid workOrderId) =>
        queue.Enqueue(new BackgroundJob("atölye: teklif kararı", (services, ct) =>
            services.GetRequiredService<WorkshopEmails>().QueueQuoteDecisionAsync(shopId, workOrderId, ct)));
}

/// <summary>
/// Atölye e-postaları. Arka planda oturum olmadan çalışır: kiracı filtresi kapalıdır, her sorgu atölye kimliğiyle süzülür.
/// Alıcılar: atölyenin sahipleri ve iş emrinin şubesindeki personel (e-postası doğrulanmış, ayrılmamış).
/// </summary>
internal sealed class WorkshopEmails(TamirhaneDbContext db, EmailOutbox outbox, AccountOptions options)
{
    public async Task QueueQuoteDecisionAsync(Guid shopId, Guid workOrderId, CancellationToken ct)
    {
        var order = await db.WorkOrders.IgnoreQueryFilters().AsNoTracking()
            .Include(o => o.QuoteLines)
            .SingleOrDefaultAsync(o => o.Id == workOrderId && o.ShopId == shopId, ct);
        if (order?.QuoteDecision is not { } decision)
            return;
        var shop = await db.Shops.IgnoreQueryFilters().AsNoTracking().SingleAsync(s => s.Id == shopId, ct);
        if (shop.IsDemo)
            return; // demo hesaplarının adresi gerçek değil
        var device = await db.Devices.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.Id == order.DeviceId && d.ShopId == shopId)
            .Select(d => d.Brand + " " + d.Model).SingleAsync(ct);

        var recipients = await db.Staff.IgnoreQueryFilters()
            .Where(s => s.ShopId == shopId && s.RemovedAt == null && (s.Role == StaffRole.Owner || s.BranchId == order.BranchId))
            .Join(db.Users, s => s.UserId, u => (Guid?)u.Id, (_, u) => u)
            .Where(u => u.EmailConfirmed && u.Email != null)
            .Select(u => u.Email!)
            .Distinct()
            .ToListAsync(ct);

        var link = new Uri(options.PublicOrigin, $"/work-orders/{order.Id}");
        var locale = shop.Locale == "en" ? "en" : "id";
        var culture = CultureInfo.GetCultureInfo(locale == "en" ? "en-GB" : "id-ID");
        if (culture.NumberFormat.CurrencyDecimalDigits != 0)
        {
            culture = (CultureInfo)culture.Clone();
            culture.NumberFormat.CurrencySymbol = "Rp ";
            culture.NumberFormat.CurrencyDecimalDigits = 0;
            culture.NumberFormat.CurrencyGroupSeparator = ".";
            culture.NumberFormat.CurrencyDecimalSeparator = ",";
        }
        var total = QuoteTotals.Of(order.QuoteLines).Total.ToString("C", culture);
        var at = decision.At.ToOffset(TimeSpan.FromHours(7));
        foreach (var to in recipients)
            outbox.Enqueue(QuoteDecisionEmail(to, locale, order.Number, device, decision, total, at, link), "keputusan estimasi");
    }

    internal static EmailMessage QuoteDecisionEmail(
        string to, string locale, string number, string device, QuoteDecision decision, string total, DateTimeOffset at, Uri link)
    {
        var approved = decision.Kind == QuoteDecisionKind.Approved;
        if (locale == "en")
        {
            var when = at.ToString("d MMMM HH:mm", CultureInfo.GetCultureInfo("en-GB"));
            return EmailLayout.Build(to, "en",
                approved ? $"{number}: customer approved the repair" : $"{number}: customer declined the repair",
                [approved
                    ? $"The customer approved the {total} quote for {device} on the tracking page ({when}, as {decision.ByName})."
                    : $"The customer declined the {total} quote for {device} on the tracking page ({when}, as {decision.ByName}).",
                 approved ? "You can start the repair." : "Return the device without repair; any diagnosis fee is shown on the work order."],
                new EmailAction("Open the work order", link),
                "You get this email because you are an owner of this shop or work at this branch.");
        }

        var id = at.ToString("d MMMM HH:mm", CultureInfo.GetCultureInfo("id-ID"));
        return EmailLayout.Build(to, "id",
            approved ? $"{number}: pelanggan menyetujui perbaikan" : $"{number}: pelanggan menolak perbaikan",
            [approved
                ? $"Pelanggan menyetujui estimasi biaya {total} untuk {device} melalui halaman pelacakan ({id}, atas nama {decision.ByName})."
                : $"Pelanggan menolak estimasi biaya {total} untuk {device} melalui halaman pelacakan ({id}, atas nama {decision.ByName}).",
             approved ? "Anda dapat mulai perbaikan." : "Kembalikan perangkat tanpa perbaikan; biaya pengecekan jika ada akan terlihat di order servis."],
            new EmailAction("Buka order servis", link),
            "Anda menerima email ini karena Anda pemilik bengkel atau staf di cabang ini.");
    }
}
