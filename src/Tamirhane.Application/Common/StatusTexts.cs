using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.Common;

/// <summary>
/// Teks notifikasi status pelanggan: email, push browser, dan pesan WhatsApp.
/// Default Indonesia; Inggris dipakai saat locale "en".
/// </summary>
public static class StatusTexts
{
    public static (string Headline, string Lead) For(WorkOrderStatus status, string locale) =>
        locale == "en" ? English(status) : Indonesian(status);

    private static (string Headline, string Lead) Indonesian(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Received => ("unit diterima", "Perangkat Anda sudah diterima di bengkel. Anda dapat memantau statusnya lewat tautan di bawah."),
        WorkOrderStatus.Diagnosing => ("sedang dicek", "Perangkat Anda sedang dicek/didiagnosa. Kami akan mengabari estimasi biaya setelah masalah ditemukan."),
        WorkOrderStatus.AwaitingApproval => ("estimasi siap", "Estimasi biaya sudah siap. Anda bisa melihat dan menyetujui atau menolaknya di halaman pelacakan."),
        WorkOrderStatus.InRepair => ("sedang diperbaiki", "Perangkat Anda sedang diperbaiki. Kami akan mengabari saat selesai."),
        WorkOrderStatus.Ready => ("siap diambil", "Perangkat Anda sudah siap diambil. Silakan bawa tanda terima saat pengambilan."),
        WorkOrderStatus.Declined => ("perbaikan tidak dilanjutkan", "Perbaikan tidak dilanjutkan. Anda dapat mengambil perangkat Anda di bengkel."),
        WorkOrderStatus.Delivered => ("sudah diserahkan", "Perangkat Anda sudah diserahkan. Informasi garansi tersedia di halaman pelacakan."),
        WorkOrderStatus.Cancelled => ("order servis dibatalkan", "Order servis ini dibatalkan. Jika ada kendala, silakan hubungi bengkel."),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private static (string Headline, string Lead) English(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Received => ("received", "Your device is at the shop and will be checked in turn; you can follow it with the link below."),
        WorkOrderStatus.Diagnosing => ("being checked", "Your device is being checked. We will let you know the price once we find the fault."),
        WorkOrderStatus.AwaitingApproval => ("quote ready", "The repair quote is ready. You can see it and approve or decline it on the tracking page."),
        WorkOrderStatus.InRepair => ("being repaired", "Your device is being repaired. We will let you know when it is done."),
        WorkOrderStatus.Ready => ("ready for pick-up", "Your device is ready. Bring your receipt and pick it up."),
        WorkOrderStatus.Declined => ("no repair", "The repair will not be done. You can come and pick up your device."),
        WorkOrderStatus.Delivered => ("picked up", "Your device has been handed over. Warranty details are on the tracking page."),
        WorkOrderStatus.Cancelled => ("work order cancelled", "This work order was cancelled. If you think something is wrong, call the shop."),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
