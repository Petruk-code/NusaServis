## Konteks Proyek & Batasan Teknis (PENTING)
- **Arsitektur:** ASP.NET Core MVC (Clean Architecture: `Domain`, `Application`, `Infrastructure`, `Web`) dengan PostgreSQL (Neon Cloud).
- **Runtime:** SDK terinstal adalah **.NET 11** dengan target proyek `net10.0` (`DOTNET_ROLL_FORWARD=Major` aktif di environment).
- **Guardrails (Larangan Keras):**
  1. **JANGAN** mengubah nama variabel C#, nama properti entity/model, nama tabel database, atau membuat migrasi EF Core baru.
  2. **JANGAN** meng-hardcode nama toko tertentu (misal "Absolute Komputer"); biarkan tetap multi-tenant agar bisa digunakan untuk banyak klien.
  3. **JANGAN** mengubah struktur key JSON pada file lokalisasi; setiap key di `id.json` harus 100% cocok dengan `en.json` dan `tr.json`.
  4. **JANGAN** menghapus file `LICENSE` asli atau komentar hak cipta `MrcDprm`.

---

# Rencana Lokalisasi & Adaptasi Indonesia (NusaServis / NusaServis)

Status: sudah diterapkan pada branch `feat/NusaServis`. Ringkasan untuk pengguna ada di [README.id.md](../README.id.md).

Saya ingin mengadaptasi proyek ASP.NET Core MVC (`NusaServis-repair-tracker`) ini agar siap digunakan untuk **pusat servis komputer, laptop, dan perangkat IT** di **Indonesia** (tetap multi-tenant tanpa meng-hardcode nama toko tertentu sehingga bisa digunakan oleh klien lain).

Lakukan analisis codebase terlebih dahulu, lalu eksekusi rencana kerja berikut secara bertahap dan rapi:

### 1. Kamus Bahasa Indonesia (`src/NusaServis.Web/Resources/id.json`)
- Baca seluruh isi `src/NusaServis.Web/Resources/en.json` dan `src/NusaServis.Web/Resources/tr.json`.
- Buat file baru **`src/NusaServis.Web/Resources/id.json`** dengan struktur key yang 100% sama.
- Terjemahkan seluruh teks ke **Bahasa Indonesia yang natural, ramah, dan profesional**, menggunakan kosakata operasional servis komputer & IT:
  - *Work order* -> **Nota Servis / Order Servis**
  - *Device* -> **Unit / Perangkat**
  - *Check in a device* -> **Terima Unit Servis**
  - *Complaint / Issue* -> **Keluhan / Kerusakan**
  - *Diagnosing* -> **Pengecekan / Diagnosa**
  - *Quote* -> **Estimasi Biaya**
  - *Parts / Inventory* -> **Sparepart / Stok**
  - *Technician / Staff* -> **Teknisi / Staf**
  - *Promised pickup date* -> **Estimasi Selesai**
- **Penyesuaian Konteks Hukum & Lokal Indonesia di `id.json`:**
  - Ubah istilah regulasi privasi Turki **KVKK** (`field.kvkk_consent`, `legal.privacy.title`, `seo.privacy.description`, `mcp.consent.kvkk_*`) menjadi **Kebijakan Privasi / Pelindungan Data Pribadi (UU PDP)**.
  - Ubah istilah pajak **VAT** (`quote.vat`, `field.default_vat`, dll.) menjadi **PPN**.
  - Ubah contoh nomor telepon Turki `0532 123 45 67` (`list.no_match_text`, `error.phone`) menjadi contoh nomor Indonesia **`0812 3456 7890`**.
  - Ubah contoh nama `Ms Kaya` (`mcp.connections.examples`) menjadi nama lokal Indonesia (misal: `Bapak Budi`).
- Periksa `src/NusaServis.Web/NusaServis.Web.csproj` untuk memastikan `Resources\id.json` ter-embed sebagai `<EmbeddedResource>` (jika belum tercakup wildcard).

### 2. Pembaruan Sistem Lokalisasi & Mata Uang Rupiah (`src/NusaServis.Web/Localization/`)
- **`AppCultures.cs`**:
  - Tambahkan `public static readonly CultureInfo Indonesian = CreateIndonesian();` berbasis `"id-ID"` dengan `CurrencySymbol = "Rp "` dan `CurrencyDecimalDigits = 0` (tanpa sen).
  - Ubah `CreateEnglish()` agar `CurrencySymbol = "Rp "` dan `CurrencyDecimalDigits = 0` (sehingga tampilan bahasa Inggris di `/en` tetap menggunakan mata uang Rupiah).
  - Ubah `All` menjadi `[Indonesian, English]` dan tambahkan dukungan kode `"id"` / `"id-"` pada method `From()`.
- **`JsonStringLocalizer.cs`**:
  - Pada `TextCatalog.Load()`, muat `"id"` dan `"en"` (serta `"tr"` jika masih dipakai sebagai fallback cadangan).
  - Jadikan `"id"` sebagai bahasa default utama di properti `Language` dan `Lookup()`.
- **`LanguagePrefix.cs`**:
  - Jadikan `AppCultures.Indonesian` sebagai budaya default untuk URL tanpa prefix (`/`), sedangkan `/en` tetap digunakan untuk `AppCultures.English`.
- Periksa `Program.cs`, `LanguageController.cs`, dan komponen pemilih bahasa di `_Layout.cshtml` agar tombol ganti bahasa menampilkan **Indonesia** dan **English** dengan benar.

### 3. Audit Validasi Telepon, WhatsApp, Zona Waktu, & Template Email
- Cari validasi format nomor telepon di kode C# (`src/NusaServis.Application`, `src/NusaServis.Domain`, `src/NusaServis.Web`) dan generator link WhatsApp (`wa.me`). Pastikan menerima format nomor Indonesia (`08...`, `+628...`, `628...`) dan mengonversinya dengan benar ke format `628...` untuk link WhatsApp.
- Periksa pengaturan zona waktu tampilan tanggal/jam dan sesuaikan ke **WIB (`Asia/Jakarta` / `SE Asia Standard Time`)** jika masih mengarah ke `Europe/Istanbul`.
- Periksa `src/NusaServis.Infrastructure` jika ada template email (verifikasi akun, reset password, undangan staf) yang masih hardcoded dalam bahasa Turki/Inggris, lalu sesuaikan ke Bahasa Indonesia.
- **PENTING:** Jangan mengubah nama variabel C#, nama properti database/Entity Framework, nama tabel, atau route URL teknis agar tidak merusak migrasi database PostgreSQL yang sudah ada.
- Periksa `src/NusaServis.Infrastructure/Email/` dan `src/NusaServis.Infrastructure/Notifications/` (khususnya `CustomerStatusEmails` dan `WorkshopEmails`) untuk menerjemahkan subjek dan isi email notifikasi servis ke Bahasa Indonesia.
- Periksa `src/NusaServis.Infrastructure/Demo/` (`DemoShops`) agar data contoh pada mode demo menggunakan contoh perangkat komputer/IT dalam Bahasa Indonesia.
- Periksa `src/NusaServis.Infrastructure/Notifications/` (seperti `WorkshopNotifier.cs`, `CustomerStatusEmails.cs`, `CustomerPushNotifications.cs`) dan `src/NusaServis.Infrastructure/Email/`:
  - Ubah mata uang yang di-hardcode `CultureInfo.GetCultureInfo("tr-TR")` menjadi format Rupiah Indonesia (`Rp #,##0` / `id-ID` tanpa desimal).
  - Ubah zona waktu `TimeSpan.FromHours(3)` (UTC+3 Istanbul) menjadi **`TimeSpan.FromHours(7)` (UTC+7 / WIB)** untuk waktu Indonesia.
  - Tambahkan template Bahasa Indonesia (`locale == "id"` atau sebagai default menggantikan `"tr"`) untuk seluruh notifikasi email & web push.
- Periksa `src/NusaServis.Infrastructure/Demo/` (`DemoShops.cs`) dan proses registrasi toko (`Shop` creation) agar nilai default `Locale` toko baru adalah `"id"` serta data contoh mode demo menggunakan perangkat komputer/IT dalam Bahasa Indonesia.


### 4. Kepatuhan Lisensi AGPL-3.0
- Pertahankan file `LICENSE` asli serta seluruh komentar hak cipta pembuat asli (`MrcDprm`) di dalam kode.
- Tambahkan keterangan kecil di bagian footer layout utama (`_Layout.cshtml`) yang mencantumkan atribusi ke proyek asli (`NusaServis - AGPL-3.0`) beserta link placeholder menuju repositori source code modifikasi ini.

### 5. Verifikasi Kompilasi
- Jalankan `dotnet build` untuk memastikan seluruh perubahan berhasil dikompilasi tanpa error dan `id.json` terbaca dengan sempurna.
- Berikan ringkasan lengkap seluruh file yang dibuat atau diubah.
