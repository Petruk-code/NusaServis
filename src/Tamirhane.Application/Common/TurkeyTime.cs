namespace Tamirhane.Application.Common;

/// <summary>
/// Zona waktu operasional bengkel: WIB (UTC+7, Asia/Jakarta). Server bisa UTC, tetapi tahun nomor servis
/// mengikuti kalender lokal (31 Desember 23:30 UTC = 1 Januari WIB, masuk tahun baru).
/// Nama kelas dipertahankan untuk kompatibilitas; nilainya sekarang WIB.
/// </summary>
public static class TurkeyTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateTimeOffset Now(TimeProvider clock) => clock.GetUtcNow().ToOffset(Offset);
}
