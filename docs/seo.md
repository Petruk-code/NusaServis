# SEO (#42)

Tujuan: seseorang yang mencari perangkat lunak pelacakan kerja untuk bengkel perbaikan menemukan OpenRespairPos / Tamirhane. Hanya halaman promosi dan legal yang masuk indeks (`/`, `/privacy`, `/terms` dan padanan `/en/...`); aplikasi, halaman akun, dan tautan pelacakan pelanggan tidak masuk.

## Kata kunci

**Bahasa Indonesia**

- Umum (kompetitif): program servis teknis, program pelacakan servis, program pelacakan perbaikan, perangkat lunak servis teknis
- Yang mencari gratis: program servis teknis gratis, program pelacakan servis gratis, program pelacakan perbaikan gratis
- Jenis bengkel: program bengkel perbaikan ponsel, program servis ponsel, program pelacakan perbaikan komputer, program servis peralatan rumah tangga
- Inti pekerjaan: program pelacakan pesanan kerja, program tanda terima penerimaan perangkat, program nota servis, program pelacakan kerusakan, persetujuan penawaran
- Sisi pelanggan: halaman pelacakan perangkat pelanggan, pengecekan status perbaikan, tautan pelacakan status servis

**English**

- General: repair shop software, repair shop management software, repair ticketing software, repair tracking software
- Free: free repair shop software, free repair shop management software, free repair ticket system
- Shop type: phone repair shop software, cell phone repair software, computer repair shop software, appliance repair software
- The job: work order tracking for repair shops, device check-in receipt, repair quote approval, parts inventory for repair shop
- Customer side: repair status tracking page, customer repair status updates

Kata-kata masuk ke dalam teks sebagai kalimat alami, bukan ditumpuk atau diisi begitu saja.

## Di halaman

| Tempat | Bahasa Indonesia | English |
|---|---|---|
| Judul (`/`) | Tamirhane: Software bengkel gratis dan pelacakan servis | Tamirhane: Free repair shop software and repair tracking |
| Deskripsi (`/`) | Perangkat lunak servis gratis untuk bengkel perbaikan: tanda terima penerimaan perangkat, pesanan kerja, persetujuan penawaran, stok suku cadang; pelanggan memantau perangkatnya dari ponsel. | Free repair shop software: check-in receipts, work orders, quote approval and parts stock. Customers follow their device's repair on their phone. |
| `h1` | Pelacakan pesanan kerja untuk bengkel perbaikan | Work order tracking for repair shops |

- Setiap halaman memiliki `<title>`, meta deskripsi (lebih pendek dari 160 karakter, diperiksa tes), canonical, `hreflang` (`id`, `en`, `x-default`; `x-default` mengarah ke URL Bahasa Indonesia), Open Graph dan kartu Twitter (`/img/og-id.png`, `/img/og-en.png`, `/img/og-tr.png`, 1200×630). Tes halaman depan mengharapkan `og-id` untuk `/` dan `og-en` untuk `/en/`. Setiap halaman hanya satu `h1`.
- Halaman promosi memuat JSON-LD `SoftwareApplication` (gratis, web, dua bahasa).
- Bahasa tercermin di alamat: Bahasa Indonesia di `/` (tanpa prefiks), English di `/en`. Bahasa browser tidak memilih alamat; mesin pencari melihat satu bahasa per alamat. Resource Turki (`tr.json`) hanya untuk kompatibilitas data lama, bukan prefiks UI Turki publik.
- `robots.txt` menutup jalur aplikasi; `sitemap.xml` mencantumkan enam alamat beserta padanan kedua bahasanya (`/`, `/en/`, `/privacy`, `/en/privacy`, `/terms`, `/en/terms`).
- Halaman aplikasi, akun, dan error menggunakan `noindex`; halaman pelacakan menggunakan `noindex, nofollow` dan `X-Robots-Tag`.

## Teks di repo dan di luar

- Deskripsi GitHub: "Free repair shop software: work orders, check-in receipts, quote approval, parts stock and a
  customer tracking page. ASP.NET Core MVC, Clean Architecture."
- Topics: repair-shop, repair-shop-software, work-order, ticketing, aspnet-core, clean-architecture, csharp, postgresql,
  htmx, multi-tenant, web-push
- Paragraf pertama `README.md` (kanonik berbahasa Inggris) memuat ungkapan yang sama (#41); `README.id.md` adalah versi Bahasa Indonesia dan `README.tr.md` versi Turki, keduanya ditautkan dari `README.md` dan `README.tr.md`.

## Search Console (dilakukan pengguna)

1. https://search.google.com/search-console → Tambah properti → **Domain** `miracdeprem.com` jika sudah terverifikasi, subdomain tamirhane akan muncul di bawahnya; jika tidak, **Prefiks URL** `https://tamirhane.miracdeprem.com/`.
2. Jika prefiks URL dipilih, untuk verifikasi sebaiknya gunakan **DNS (catatan TXT)**, bukan tag HTML (ditambahkan ke DNS Vercel).
3. Pengindeksan → Peta situs → kirim `sitemap.xml`.
4. Inspeksi URL → untuk `https://tamirhane.miracdeprem.com/` dan `/en/`, pilih "Minta pengindeksan".
5. Beberapa hari kemudian, di laporan Halaman, periksa bahwa kedua bahasa berstatus "Terindeks" dan tidak ada peringatan hreflang.
