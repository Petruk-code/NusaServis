# NusaServis: rencana v1

Sistem pelacakan pesanan kerja untuk bengkel servis dan perbaikan. ASP.NET Core MVC, Clean Architecture, PostgreSQL. Lisensi AGPL-3.0. Tidak ada rencana masa depan: semua hal di bawah selesai sebelum v1.0.0. Fitur yang memerlukan biaya atau perusahaan (SMS, faktur e-Arsip, pembayaran online) tidak termasuk cakupan. Di fork/cabang ini produk bernama NusaServis / NusaServis (cabang `feat/NusaServis`).

## Siapa, di mana, untuk apa

- **Pemilik bengkel dan teknisi:** Toko beranggotakan 1-10 orang yang memperbaiki ponsel, komputer, tablet, dan peralatan rumah tangga kecil. Mereka bekerja sambil berdiri di meja konter, di siang hari atau di bawah lampu neon; mereka ingin mencatat perangkat saat pelanggan masih menunggu, atau menemukan pekerjaan pelanggan yang datang bertanya "bagaimana keadaan perangkat saya?" dalam beberapa detik.
- **Pelanggan:** Sudah menitipkan perangkatnya dan membuka tautan pelacakan dari ponselnya. Ia tidak ingin membuat akun; yang ia inginkan hanyalah "bagaimana statusnya, berapa biayanya, kapan saya bisa mengambilnya".
- **Pesaing:** RepairShopr (~60 $/pengguna/bulan), RepairDesk (99-149 $/toko/bulan), Bilsoft di Turki dan perangkat lunak desktop/cloud serupa. Inti yang sama: penerimaan perangkat, status pesanan kerja, notifikasi pelanggan, persetujuan penawaran, stok suku cadang, tanda terima penerimaan. Pembeda: versi yang dihosting secara gratis, halaman pelacakan tanpa login, Bahasa Indonesia, sumber terbuka.

## Cakupan

**Bengkel dan pengguna**
- Pendaftaran multi-tenant: data setiap bengkel sepenuhnya terpisah.
- Cabang: stok dan pesanan kerja terikat pada cabang; bengkel dengan satu cabang tidak pernah melihatnya.
- Peran: pemilik (semua akses) dan teknisi (pesanan kerja, pemakaian stok; tanpa pengaturan, staf, dan omzet). Staf diundang lewat email.
- Pendaftaran dengan verifikasi email, pengaturan ulang kata sandi, penghapusan akun dan bengkel.

**Penerimaan perangkat**
- Pelanggan: nama, telepon, email (opsional), persetujuan pemberitahuan UU PDP. Pencarian berdasarkan nomor telepon; pelanggan dengan nomor yang sama tidak diketik ulang.
- Perangkat: jenis, merek, model, nomor seri / IMEI (pemeriksaan Luhn pada IMEI), aksesori yang diterima, kondisi tampak (goresan, retak...), keluhan pelanggan, informasi kunci/kata sandi layar opsional dan dihapus saat pesanan kerja ditutup.
- Tanda terima A5 yang dapat dicetak (fitur cetak browser, CSS `@page`; tanpa pustaka PDF): informasi bengkel, perangkat, keluhan, ketentuan, kolom tanda tangan, dan kode QR ke tautan pelacakan.
- Label yang ditempelkan pada perangkat: nomor pesanan kerja + kode QR, berukuran sesuai printer label kecil.

**Pesanan kerja**
- Alur status (di bawah); setiap perubahan dicatat dengan waktu dan staf; perubahan tidak valid ditolak di lapisan domain.
- Penawaran: baris suku cadang (dipilih dari stok) dan jasa, tarif PPN per baris (default 10%), diskon. Pelanggan menyetujui atau menolak lewat tautan pelacakan; nama dan waktu penyetuju dicatat.
- Stok suku cadang: jumlah per cabang, batas kritis; suku cadang yang terpakai saat perbaikan dikurangi dari stok, dikembalikan saat pembatalan. Daftar pergerakan stok.
- Pembayaran: tunai, kartu (POS), transfer; pembayaran sebagian, sisa tagihan. Uang tidak melewati sistem, hanya dicatat.
- Garansi: masa garansi mulai saat pengambilan (pengaturan bengkel, default 180 hari). Perangkat yang kembali dalam masa garansi ditautkan ke pesanan kerja baru sebagai "kembali garansi"; jasa disarankan gratis.
- Catatan teknisi (tidak terlihat pelanggan) dan catatan yang terlihat pelanggan terpisah.

**Komunikasi dengan pelanggan**
- Halaman pelacakan tanpa login: `/t/{kod}`. Linimasa status, penawaran dan tombol persetujuan, perkiraan pengambilan, alamat dan telepon bengkel.
- Email pada setiap perubahan status (Resend, paket gratis).
- Notifikasi browser (Web Push, VAPID): pelanggan memilih "beri tahu saya saat status berubah" di halaman pelacakan.
- Tombol "Beri tahu lewat WhatsApp": membuka tautan `wa.me` dengan pesan siap pakai + tautan pelacakan; teknisi mengirim dengan satu klik.

**Panel dan umum**
- Pekerjaan terbuka, menunggu persetujuan, siap diambil, menunggu lebih dari 30 hari, omzet bulanan (khusus pemilik), stok kritis.
- Pencarian cepat: nama, telepon, IMEI, nomor pesanan kerja.
- Bahasa Indonesia (default, `/`) dan English (`/en`), tema terang dan gelap.
- Bengkel demo untuk umum (#36): setiap pengunjung yang memilih "Coba demo" mendapat bengkel terpisah dengan data contoh dan masuk sebagai pemiliknya (bukan hanya-baca, tidak ada yang melihat demo orang lain). Demo berumur 24 jam, lalu dihapus beserta seluruh data dan akunnya. Demo tidak mengirim email keluar (notifikasi pelanggan, notifikasi bengkel, undangan); alamat akun demo menggunakan akhiran `.invalid`. Ada batas pembukaan per IP dan batas jumlah demo yang aktif secara bersamaan.
- Halaman promosi, halaman legal `/privacy` (kebijakan privasi / pemberitahuan UU PDP) dan `/terms` (ketentuan penggunaan) beserta padanan Englishnya.

## Arsitektur

```
src/
  Tamirhane.Domain          entitas, objek nilai, aturan bisnis, kesalahan domain
  Tamirhane.Application     kasus penggunaan, antarmuka (repositori, jam, email, push, pengguna/bengkel saat ini)
  Tamirhane.Infrastructure  EF Core + Npgsql, Identity, Resend, Web Push, pembuatan QR
  Tamirhane.Web             controller MVC, view, autentikasi, lokalisasi, header keamanan
tests/
  Tamirhane.Domain.Tests        aturan (mesin status, total penawaran, stok, garansi)
  Tamirhane.Application.Tests   kasus penggunaan dengan repositori palsu
  NusaServis.Architecture.Tests  aturan ketergantungan (NetArchTest) + integrasi (WebApplicationFactory + Testcontainers PostgreSQL)
```

Saat ini ada 748 test.

**Aturan ketergantungan:** Domain tidak bergantung pada proyek mana pun dan paket NuGet mana pun. Application hanya bergantung pada Domain; tidak mengenal EF Core, ASP.NET, atau Identity. Infrastructure mengimplementasikan antarmuka di Application. Web mereferensikan Infrastructure hanya di `Program.cs` untuk pendaftaran layanan (composition root). Tes arsitektur menangkap pelanggaran aturan ini bukan saat kompilasi, melainkan saat pengujian:
- Domain → tidak boleh ada proyek `NusaServis.*` maupun `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore`.
- Application → tidak boleh ada `Infrastructure`, `Web`, `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore`.
- Controller tidak menggunakan tipe `Tamirhane.Infrastructure`.

**Keputusan:**
- **Tanpa MediatR:** pada 2025 paket ini beralih ke lisensi komersial. Setiap kasus penggunaan adalah satu kelas (seperti `ReceiveDeviceHandler`), disuntikkan langsung di Web. Validasi ada di dalam kasus penggunaan; tanpa pustaka eksternal.
- **Tipe hasil:** Kesalahan yang diharapkan (perubahan tidak valid, stok kurang, tidak ditemukan) mengembalikan `Result<T>`, bukan exception; controller menerjemahkannya ke respons HTTP dan pesan pengguna yang tepat. Kesalahan tak terduga menuju halaman error umum.
- **Domain kaya:** Perubahan status bukan `workOrder.Status = ...`, melainkan lewat metode seperti `workOrder.StartRepair(technician, now)`; aturan ada di dalam entitas. Setter bersifat `private`.
- **Abstraksi waktu:** `TimeProvider` milik .NET; aturan garansi dan "lebih dari 30 hari" diuji dengan jam palsu.
- **Multi-tenant:** Setiap tabel milik tenant memiliki `ShopId`. Filter query global EF Core berasal dari `ICurrentShop`; saat menulis, `ShopId` ditetapkan di server, tidak pernah diambil dari form. Tes integrasi: pengguna bengkel A meminta pesanan kerja bengkel B berdasarkan ID → 404.
- **ID:** Semua kunci primer adalah `Guid` (v7, berurutan). Nomor pesanan kerja yang terlihat di layar berurutan per bengkel (`2026-0042`); tidak pernah dipakai untuk akses, hanya untuk keterbacaan.
- **Kode pelacakan:** 12 karakter Crockford base32 (60 bit acak), indeks unik di database; permintaan dengan kode dibatasi per IP.
- **Uang:** `decimal(18,2)`, Rupiah (simbol `Rp `, 0 angka desimal untuk tampilan). PPN per baris; total dihitung di domain, total dari form tidak dipakai.
- **Pencetakan:** Tanda terima penerimaan dan label menggunakan halaman cetak terpisah (`@media print`, `@page size: A5`); tanpa pustaka PDF maupun render sisi server.
- **QR:** QRCoder (MIT), keluaran SVG.
- **Web Push:** paket NuGet `Lib.Net.Http.WebPush` (MIT; tidak memerlukan pustaka kripto tambahan di .NET 10), kunci VAPID di variabel lingkungan. (Rencana awal menulis `WebPush`; paket itu berlisensi MPL-2.0 dan bergantung pada Portable.BouncyCastle yang tidak diperbarui sejak 2021, sehingga diubah di #31.)
- **Tes integrasi:** Testcontainers membuka PostgreSQL sementara di Docker pada setiap menjalankan tes dan menghapusnya saat selesai. Database sungguhan tidak pernah disentuh; filter tenant dan migrasi diuji pada PostgreSQL sungguhan. Docker Desktop diperlukan (keputusan 2026-10-20: akan dipasang).
- **Pekerjaan latar belakang:** Pengiriman email dan push berada dalam antrean `BackgroundService` berbasis `Channel`; permintaan tidak menunggu, kesalahan dicatat dan dicoba kembali 3 kali.

## Antarmuka Bahasa Indonesia dan English (#35)

- **Pemilihan bahasa:** Bahasa Indonesia adalah bahasa default di `/`, English berada di `/en`. Di panel bengkel, pemilih ID/EN di bilah atas disimpan dalam cookie; jika cookie tidak ada, gunakan bahasa browser (di luar Bahasa Indonesia → English). Halaman pelacakan pelanggan dibuka dalam bahasa pelanggan yang dipilih bengkel (sama seperti email, notifikasi, dan WhatsApp), dengan pengalih ID/EN kecil di halaman tersebut.
- **File resource:** `Resources/id.json` dan `en.json`, dengan kunci bermakna (`workorder.receive.title`). Kami memakai implementasi `IStringLocalizer` sendiri; view, pesan controller, dan pesan validasi (DataAnnotations) berasal dari file yang sama. `Resources/tr.json` tetap ada hanya untuk kompatibilitas data lama, bukan prefiks UI Turki publik. Jika kunci tidak ditemukan, tampilkan Bahasa Indonesia, jika masih tidak ada tampilkan kunci itu sendiri (halaman tidak rusak saat transisi).
- **Tes:** kunci yang sama di kedua file, placeholder yang sama pada setiap kunci, tidak ada nilai kosong.
- **Mata uang:** Rupiah dengan simbol `Rp ` dan 0 angka desimal untuk tampilan. Budaya English berbasis en-GB, tetapi simbol mata uang tetap `Rp ` (en-GB akan menampilkan £).

## Model data (ringkasan)

```
Shop            (Id, Name, Phone, Address, WarrantyDays, DefaultVatRate, Locale, CreatedAt)
Branch          (Id, ShopId, Name, Address)
Staff           (Id, ShopId, UserId, Role: Owner|Technician, DisplayName)
Customer        (Id, ShopId, Name, Phone, Email?, KvkkConsentAt)
Device          (Id, ShopId, CustomerId, Kind, Brand, Model, Serial?, Imei?)
WorkOrder       (Id, ShopId, BranchId, Number, TrackingCode, CustomerId, DeviceId,
                  Status, Complaint, Condition, Accessories, Passcode?, CustomerNote?,
                  EstimatedReadyAt?, DeliveredAt?, WarrantyUntil?, WarrantyOfId?, CreatedAt)
StatusChange    (Id, WorkOrderId, From, To, StaffId?, At, Note?)
QuoteLine       (Id, WorkOrderId, Kind: Part|Labor, PartId?, Description, Quantity, UnitPrice, VatRate, Discount)
QuoteDecision   (WorkOrderId, Decision: Approved|Declined, ByName, At)
Part            (Id, ShopId, Sku, Name, CostPrice, SalePrice, CriticalLevel)
StockLevel      (PartId, BranchId, Quantity)
StockMovement   (Id, PartId, BranchId, Change, Reason: Purchase|Used|Returned|Adjustment, WorkOrderId?, At)
Payment         (Id, WorkOrderId, Amount, Method: Cash|Card|Transfer, At, StaffId)
PushSubscription(Id, WorkOrderId, Endpoint, P256dh, Auth, CreatedAt)
Invitation      (TokenHash, ShopId, Email, Role, ExpiresAt)
```

Tabel Identity (`AspNetUsers`...) berada di Infrastructure; `Staff` di Domain hanya mengetahui pengguna lewat `UserId`.

## Mesin status pesanan kerja

```
Received ──► Diagnosing ──► AwaitingApproval ──► InRepair ──► Ready ──► Delivered
   │             │                │   │                         ▲
   │             │                │   └── (ditolak) ──► Declined ───┘ (perangkat dikembalikan tanpa diperbaiki)
   │             └── (tanpa penawaran, garansi/gratis) ──► InRepair
   └── dari status terbuka mana pun ──► Cancelled
```

- Untuk masuk ke `AwaitingApproval`, diperlukan setidaknya satu baris penawaran. Setelah penawaran disetujui, status dapat berpindah ke `InRepair`; jika ditolak → `Declined` (perangkat siap diambil tanpa diperbaiki, dengan biaya pemeriksaan jika ada).
- `InRepair` → `Ready`: suku cadang yang dipakai dikurangi dari stok (jika tidak cukup, perubahan tidak terjadi).
- `Ready` → `Delivered`: sisa tagihan harus nol atau pemilik memberikan persetujuan "pengambilan dengan tagihan". Garansi mulai saat pengambilan, kata sandi perangkat dihapus.
- `Cancelled` dan `Delivered` adalah status akhir; tidak ada perubahan lagi. Masalah setelah pengambilan → pesanan kerja baru (`WarrantyOfId`).
- Setiap perubahan menghasilkan baris `StatusChange` + notifikasi pelanggan (email, push).

## Kasus penggunaan (Application)

Penerimaan: `ReceiveDevice`, `FindCustomerByPhone`, `UpdateIntakeDetails`. Pesanan kerja: `StartDiagnosis`, `AddQuoteLine`, `RemoveQuoteLine`, `SendQuote`, `DecideQuote` (dari halaman pelacakan), `StartRepair`, `MarkReady`, `Deliver`, `Cancel`, `ReopenUnderWarranty`, `AddNote`. Stok: `AddPart`, `ReceiveStock`, `AdjustStock`, `ListLowStock`. Pembayaran: `RecordPayment`. Pelacakan: `GetTrackingView`, `SubscribePush`. Bengkel: `RegisterShop`, `InviteStaff`, `AcceptInvitation`, `AddBranch`, `UpdateShopSettings`, `DeleteShop`. Panel: `GetDashboard`, `Search`.

## Daftar pemeriksaan keamanan

- Identity: kata sandi minimal 10 karakter + huruf dan angka, daftar kata sandi umum; terkunci 15 menit setelah 5 kali salah; bengkel tidak dapat dipakai sebelum email terverifikasi; cookie berawalan `__Host-`, HttpOnly, Secure, SameSite=Lax; security stamp diperiksa setiap permintaan (saat kata sandi berubah, sesi lain ditutup).
- Otorisasi di server pada setiap permintaan: kebijakan peran + filter tenant. Teknisi tidak dapat mengakses endpoint omzet dan pengaturan (ada tes).
- Antiforgery pada semua POST (juga pada form bertanda `action`; tes regresi yang memindai view, dari bug di Sahaf).
- Tanpa overposting: setiap form memiliki model input sendiri; `ShopId`, status, dan total tidak pernah diambil dari form.
- Rate limit: batas umum per IP (600 per menit) + untuk login, pendaftaran, pengaturan ulang kata sandi, undangan, penghapusan bengkel, halaman pelacakan, keputusan penawaran, langganan push, dan pembukaan demo. IP klien diambil dari `X-Forwarded-For` yang ditulis Caddy, hanya jika berasal dari mesin yang sama (2026-11-03: di Render, hop terakhir adalah proxy Render sendiri, sehingga semua orang berbagi batas yang sama; setelah pindah ke Oracle hal ini membaik, dan di lingkungan produksi diuji bahwa header palsu tidak bisa menembusnya).
- Visibilitas data pribadi: di panel bengkel (staf yang sudah login), nama lengkap, telepon, dan email pelanggan terlihat di semua tempat. Halaman pelacakan tanpa login, karena tautannya dapat diteruskan, hanya menampilkan informasi minimum: dua huruf pertama dari setiap nama, sisanya bintang (seperti halaman pelacakan pengiriman), misalnya "Ayşe Kaya" → "Ay** Ka**". Jumlah huruf yang terlihat paling banyak 2, tetapi selalu ada minimal satu huruf yang disembunyikan: "Ali" → "Al*", "Su" → "S*"; masking dilakukan di server, nama lengkap tidak pernah dikirim ke halaman. Telepon dan email tidak ditampilkan sama sekali; kata sandi perangkat tidak ada di tanda terima maupun halaman pelacakan, staf melihatnya di panel setelah mengklik.
- Tautan email berasal dari pengaturan `PUBLIC_ORIGIN`, bukan header `Host` (pelajaran dari Casus); `AllowedHosts`.
- CSP: `default-src 'self'`, tanpa skrip inline, Trusted Types; HSTS, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy`, `frame-ancestors 'none'`. Halaman cetak menggunakan CSP yang sama.
- Tanpa CORS. Di produksi, halaman error umum, tanpa stack trace; log tidak berisi email, telepon, atau IP.
- Rahasia (connection string, Resend, VAPID) berada di file lingkungan pada server yang hanya dapat dibaca root; di repo ada `.env.example`.
- Kunci Data Protection (cookie sesi, token email, kata sandi perangkat) di produksi berada di disk server (systemd `StateDirectory`), bukan di database: meski database bocor, cookie palsu tidak dapat dibuat dan kata sandi perangkat tidak dapat dibuka. Kata sandi perangkat tersimpan terenkripsi di database (berawalan `dp1:`).
- Koneksi Neon menggunakan `VerifyFull`: terenkripsi dan sertifikat server diverifikasi.
- UU PDP: pemberitahuan privasi, waktu persetujuan eksplisit tercatat saat pendaftaran pelanggan, semua data dihapus saat bengkel dihapus; kata sandi perangkat dihapus pada pesanan kerja yang sudah diambil.
- Sebelum produksi, periksa dependensi dengan `dotnet list package --vulnerable`; karena paket rilis membawa runtime .NET di dalamnya, SDK diperbarui sebelum setiap rilis (versi dicatat saat aplikasi mulai).

## Sprint (1 minggu)

Setiap butir adalah issue; setiap issue memiliki branch dan PR (`Closes #n`).

1. **Inti:** Entitas Domain dan mesin status, total penawaran, aturan stok dan garansi; kasus penggunaan Application (dengan repositori palsu); tes arsitektur.
2. **Infrastruktur dan akun:** EF Core + PostgreSQL, migrasi, filter tenant, Identity, pendaftaran + verifikasi email, undangan staf, cabang, peran; tes integrasi (Testcontainers).
3. **Layar bengkel:** Penerimaan perangkat, halaman pesanan kerja (status, penawaran, catatan), stok, pembayaran, pencetakan tanda terima dan label, pencarian cepat.
4. **Sisi pelanggan:** Halaman pelacakan, persetujuan penawaran, notifikasi email, Web Push, tombol WhatsApp.
5. **Panel dan rilis:** Panel, kembali garansi, demo bengkel, halaman promosi dan legal, ID/EN, tema, pemindaian akhir, Oracle Cloud (Caddy + systemd) + Neon + Resend (2026-11-03: pindah ke Oracle karena paket gratis Render ditidurkan dan domain berbayar), README (`README.md` kanonik berbahasa Inggris, `README.id.md` Bahasa Indonesia, `README.tr.md` Turki; ditautkan dari `README.md` dan `README.tr.md`), v1.0.0, SEO.

## Rencana desain

**Tema, audiens, tugas:** Teknisi yang bekerja sambil berdiri di konter, lingkungan terang (Operate); pelanggan yang melihat dari ponsel (Read). Bahasa visual berasal dari benda-benda bengkel perbaikan itu sendiri: **label pesanan kerja dari karton** yang diikat dengan tali pada perangkat (berlubang, bernomor), stempel dan alat cap di meja konter, label di atas kotak suku cadang. Tidak ada monitor, papan sirkuit, atau klise "teknologi".

**Warna (tema terang sebagai default, bengkel terang):**

| Nama | Hex | Fungsi |
|---|---|---|
| Konter | `#ECEEEB` | Latar: abu-abu terang dingin (bukan krem) |
| Karton label | `#E6D3A8` | Hanya permukaan label pesanan kerja; tidak di tempat lain |
| Tinta | `#1F2A30` | Teks |
| Biru stempel | `#2B44A8` | Stempel status dan satu aksi utama |
| Oranye peringatan | `#C8571B` | Stok kritis, pekerjaan lebih dari 30 hari, tagihan |
| Timah | `#69707A` | Teks sekunder, garis |

Tema gelap (saat tutup sore hari): latar `#1B2024`, label `#4A4130`, teks `#E7E9E6`, stempel `#7C93F0`. Tanpa efek berkilau.

**Font (diverifikasi 2026-10-20: ketiganya ada di Google Fonts, subset `latin-ext` mencakup karakter Latin tambahan, lisensi OFL; file akan disajikan secara lokal):**
- **Barlow Semi Condensed** (OFL): judul dan nomor pesanan kerja. Sempit dan industrial; berasal dari dunia tulisan pada label dan kotak suku cadang.
- **Public Sans** (OFL): isi dan form. Mudah dibaca, netral; untuk permukaan Operate.
- **Red Hat Mono** (OFL): hanya untuk kode sungguhan (IMEI, nomor seri, kode pelacakan); tidak dipakai pada label kecil.

Tidak mengulang proyek sebelumnya: Atkinson Hyperlegible (Nabız), Bricolage Grotesque / Source Sans 3 / JetBrains Mono (Zarf), Geist (Crypto Toolkit), Fraunces / Manrope (Seans).

**Tata letak (bengkel):**

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│ NusaServis   [ Cari nama, telepon, IMEI atau no pesanan kerja........ ]  [Terima perangkat] │
├─────────────────────────────────────────────────────────────────────────────────────┤
│ Hari ini                                                                            │
│  Menunggu persetujuan 3   Siap diambil 5   Sedang diperbaiki 8   Lebih dari 30 hari 1 │  ← kalau diklik, daftar difilter
├─────────────────────────────────────────────────────────────────────────────────────┤
│ No         Perangkat     Kerusakan      Pelanggan      Status               Menunggu │
│ 2026-0042  iPhone 13     Layar retak    Ayşe Kaya      Sedang diperbaiki    2 hari   │
│ 2026-0041  Lenovo T14    Suara kipas    Mert Demir     Siap                 hari ini │  ← tabel, bukan kartu
│ 2026-0040  Dyson V11     Tidak mengisi  Selin Arslan   Menunggu persetujuan 4 hari   │
└─────────────────────────────────────────────────────────────────────────────────────┘
```

Halaman pesanan kerja: di kiri label (permukaan karton, lubang, nomor, perangkat, keluhan, stempel), di kanan kolom aksi (perubahan status berikutnya sebagai satu tombol utama, tabel penawaran, pembayaran, catatan). Rata kiri, tabel selebar penuh.

**Halaman pelacakan pelanggan (ponsel):** Di bagian paling atas label itu sendiri: nama bengkel, nomor pesanan kerja, perangkat. Di bawahnya, setiap status yang dilalui ditampilkan sebagai bekas stempel yang dicap pada label, lengkap dengan tanggal dan waktu. Jika ada penawaran, tampilkan baris-barisnya dan dua tombol polos: "Setujui perbaikan" / "Saya tidak ingin perbaikan".

**Prinsip:**
- Berani hanya di satu tempat: **stempel**. Setiap perubahan status dicap pada label dengan biru stempel, sedikit miring dan sedikit bertekstur (SVG + mask CSS, tekstur diambil dari bekas stempel sungguhan). Saat status diubah di bengkel, ada satu gerakan "mencap"; pada `prefers-reduced-motion` tidak ada animasi. Teks stempel menggunakan huruf kapital (stempel memang begitu); tidak ada label huruf kapital di tempat lain di aplikasi.
- Gerakan lain tenang: tabel, tombol polos, dua tingkat pembulatan sudut (label dan tombol berbeda), tidak ada kartu di dalam kartu.
- Warna hemat: biru stempel hanya untuk cap dan aksi utama; oranye hanya untuk peringatan sungguhan.
- Teks polos: "Terima perangkat", "Mulai perbaikan", "Serahkan". Daftar kosong: "Tidak ada pekerjaan terbuka hari ini. Saat ada perangkat baru, tekan Terima perangkat."
- Target sentuh minimal 44 px (di konter, dengan tangan bersarung); pintasan keyboard (`/` untuk mencari, `N` untuk penerimaan baru).

**Pemeriksaan terhadap default (revisi):**
- Gagasan pertama adalah tampilan "latar gelap + oranye neon, servis teknis = teknologi" (klise nomor dua). Diubah: benda-benda bengkel sendiri (label karton, stempel), latar terang.
- Kandidat kedua adalah menampilkan pesanan kerja sebagai papan kanban berisi kart-kart sudut bulat (kit kart SaaS). Diubah: daftar berupa tabel; tampilan "label" hanya pada halaman satu pesanan kerja dan halaman pelanggan.
- Agar warna karton label tidak bergeser ke krem, warnanya dibuat lebih jenuh, mendekati karton manila sungguhan, dan tidak pernah dipakai sebagai latar.

**Revisi 2 (2026-10-26): papan berlubang dan lapisan animasi.** Layar awal hanya berupa form dan tabel polos; tidak terasa seperti bengkel. Warna, font, dan stempel tetap sama, ditambah:
- **Elemen khas papan berlubang (pegboard):** menu samping berupa papan gelap berlubang; halaman yang sedang dibuka adalah label karton yang digantung di papan. Di bawahnya tergantung alat-alat (obeng, tang, pinset).
- **Halaman masuk, daftar, kata sandi, dan undangan dibagi dua:** di kiri gambar papan (alat tergantung, ponsel dengan layar retak, label pesanan kerja dan stempel yang diikat dengan tali, konter), di kanan form.
- **Panel:** halaman berisi panel di atas latar konter (berjudul, berbayang); tidak ada kartu di dalam kartu. Angka-angka panel (#33) adalah label yang digantung dengan tali di papan. Saat kosong, papan menampilkan gambar kait kosong.
- **Layar penerimaan:** langkah bernomor (pelanggan, perangkat, kerusakan) dan di kanan pratinjau label yang terisi saat Anda mengetik.
- **Animasi (Motion, MIT; GSAP tidak dipilih karena tidak cocok dengan lisensi AGPL):** gambar papan hidup sekali (alat tergantung, label bergoyang di talinya, stempel dicap), label di papan bergoyang saat digantung, stempel dicap dengan efek pegas, alat di menu samping bergoyang saat dihover. Transisi halaman dengan View Transitions (label dari penerimaan "berpindah" ke label di halaman pesanan kerja; di Firefox halaman dibuka biasa). Pembaruan sebagian halaman dengan htmx di #22 (saat status berubah, hanya stempel yang dicap pada label). Jika pengurangan animasi aktif, tidak ada yang berjalan.
- Tidak ada animasi di semua tempat: gerakan klise seperti bagian yang muncul dengan meluncur sengaja tidak dipakai.

## v1.1.0: Akses dari asisten kecerdasan buatan (MCP)

Pemilik bengkel atau teknisi dapat berkata kepada Claude miliknya (web, seluler, desktop): "bagaimana status 2026-0042", "suku cadang dengan stok kritis", "yang siap diambil hari ini", "tulis pesan untuk Bapak Budi bahwa perangkatnya sudah siap". Model bahasa dijalankan oleh klien pengguna sendiri; NusaServis tidak menanggung biaya API maupun kunci API.

**Arsitektur**
- MCP adalah lapisan presentasi kedua di samping MVC: dengan SDK C# resmi (`ModelContextProtocol.AspNetCore`) di aplikasi yang sama pada `/mcp` (Streamable HTTP). Alat memanggil kasus penggunaan Application yang sudah ada; filter tenant, peran, dan mesin status tetap berlaku. Tidak ada server terpisah, tetap VM Oracle dan Caddy yang sama.
- **OAuth 2.1:** NusaServis menjadi server otorisasinya sendiri (OpenIddict, Apache-2.0, di atas ASP.NET Identity). Authorization code + PKCE (S256), access token berumur pendek, refresh token yang berputar. Login memakai halaman masuk yang ada; setelah itu layar izin ("Claude dapat membaca pesanan kerja bengkel ini dan mengubah statusnya"). Token terikat pada satu bengkel dan peran staf tersebut.
- **Penemuan (discovery):** `/.well-known/oauth-protected-resource` (RFC 9728) dan `/.well-known/oauth-authorization-server` (RFC 8414); respons 401 menyertakan alamat metadata dalam `WWW-Authenticate`. Nilai `aud` token adalah alamat MCP (RFC 8707).
- **Pendaftaran klien:** Pendaftaran dinamis (RFC 7591), jalur yang dipakai claude.ai, Claude Desktop/Code, VS Code, dan Cursor. Alamat redirect dibatasi daftar izin (https claude.ai/claude.com, `localhost`/`127.0.0.1` untuk klien lokal); rate limit terpisah untuk pendaftaran; pendaftaran lama yang tidak pernah mendapat token dihapus berkala.

**Alat**
- `find_work_order`: mencari pesanan kerja berdasarkan nomor, nama pelanggan, atau telepon; status, perangkat, keluhan, penawaran, perkiraan pengambilan.
- `list_work_orders`: berdasarkan status (menunggu, menunggu suku cadang, siap diambil, terlambat), maksimal 50 catatan.
- `low_stock`: suku cadang di bawah batas kritis, per cabang.
- `change_status`: mengubah status pesanan kerja (aturan domain tetap berlaku, perubahan tidak valid menghasilkan pesan yang jelas), catatan opsional. Klien meminta konfirmasi (`destructiveHint`).
- Tidak ada alat untuk menghapus, menerima pembayaran, atau mengubah harga. Draf pesan bukan alat: alat memberikan data, teks ditulis oleh model pengguna.
- Asisten melihat apa yang dilihat orang yang terhubung di web (teknisi tidak melihat omzet); harga beli dan kata sandi perangkat tidak dikembalikan untuk peran apa pun. Teks keluhan yang ditulis pelanggan dikembalikan di kolom terpisah sebagai "teks pelanggan" dalam keluaran alat (data, bukan instruksi, sebagai perlindungan terhadap prompt injection).

**Keamanan dan UU PDP**
- Token ditandatangani/dienkripsi dengan Data Protection, database hanya menyimpan referensi; di pengaturan ada daftar "Aplikasi terhubung" dan pencabutan sekali klik.
- Rate limit per pengguna untuk MCP, setiap panggilan alat masuk ke log audit (siapa, alat apa, pesanan kerja mana; data pelanggan tidak dicatat).
- Pada layar izin ada peringatan UU PDP: data dikirim ke penyedia kecerdasan buatan yang dipilih pengguna; bengkel harus menambahkan hal ini ke pemberitahuan privasinya.
- Bengkel demo dapat dicoba melalui MCP secara hanya-baca.

**Di luar cakupan (gratis tetapi tidak perlu):** Tidak perlu usaha khusus untuk ChatGPT; karena memakai OAuth + MCP standar, jika mendukungnya akan berjalan sendiri, diuji lalu ditulis di README.

**Issue (Sprint 3, 3-9 November):** (1) server MCP + OAuth, (2) rilis v1.1.0 (uji dengan claude.ai di produksi, README ID/EN/TR, Release).
