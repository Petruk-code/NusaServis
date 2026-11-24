Bahasa Indonesia | [English](README.md) | [Türkçe](README.tr.md)

# Tamirhane

Perangkat lunak gratis untuk bengkel perbaikan ponsel, komputer, dan perangkat rumah tangga: penerimaan perangkat dengan tanda terima yang dapat dicetak, pesanan kerja, penawaran yang disetujui pelanggan melalui tautan pelacakan, stok suku cadang, pembayaran, dan garansi. Pelanggan memantau perbaikan perangkatnya dari ponsel tanpa membuat akun. Dibangun dengan ASP.NET Core MVC dan Clean Architecture.

**Situs:** [tamirhane.miracdeprem.com](https://tamirhane.miracdeprem.com) · coba bengkel demo tanpa mendaftar ([Bahasa Indonesia](https://tamirhane.miracdeprem.com/) · [English](https://tamirhane.miracdeprem.com/en/))

## Fitur

**Penerimaan di konter**
- Temukan pelanggan lewat nomor telepon; pelanggan yang kembali beserta perangkatnya muncul lagi, tidak ada yang diketik dua kali
- Formulir menyesua jenis perangkat (12 jenis, dari ponsel hingga perangkat besar): daftar merek, saran model dari riwayat bengkel sendiri, IMEI hanya pada perangkat yang memilikinya, jenis kunci layar (PIN, pola, kata sandi)
- Kotak centang untuk kondisi yang terlihat dan aksesori yang diterima
- Tanda terima A5 yang dapat dicetak dengan kode QR ke halaman pelacakan, serta label perangkat untuk printer label

**Pesanan kerja**
- Alur status: Diterima → Diagnosa → Menunggu persetujuan → Sedang diperbaiki → Siap diambil → Sudah diambil; langkah tidak valid ditolak di domain
- Penawaran dengan suku cadang dari stok dan jasa, PPN serta diskon per baris; persetujuan dicatat dengan nama dan waktu
- Stok suku cadang per cabang dengan batas stok rendah; suku cadang terpakai keluar dari stok, yang dibatalkan masuk kembali
- Pembayaran (tunai, kartu, transfer), pembayaran sebagian dan sisa tagihan; garansi mulai saat pengambilan dan perangkat yang kembali ditautkan ke pesanan kerja lama
- Kata sandi perangkat disimpan terenkripsi dan hanya ditampilkan ke staf saat diklik; dihapus saat pesanan kerja ditutup

**Sisi pelanggan**
- Halaman pelacakan tanpa login: linimasa status, perkiraan tanggal, persetujuan atau penolakan penawaran, alamat dan telepon bengkel
- Email pada setiap perubahan status, notifikasi browser (pada iPhone setelah halaman ditambahkan ke Layar Utama), pesan WhatsApp sekali klik

**Bengkel**
- Panel: menunggu persetujuan, sedang diperbaiki, siap diambil, lebih dari 30 hari, omzet bulanan, stok rendah
- Multi-tenant: data setiap bengkel terpisah; cabang, peran pemilik dan teknisi, undangan staf lewat email
- Bahasa Indonesia (default) dan English (di bawah `/en`); resource Turkish tetap dikenali untuk data lama, tema terang dan gelap, nyaman dipakai di ponsel di konter
- Bengkel demo publik dengan data contoh, dihapus setelah 24 jam; pemilik dapat menghapus bengkel beserta seluruh datanya

**Asisten AI (MCP)**
- Hubungkan Claude (atau klien MCP lain) ke bengkel Anda dan tanya dengan bahasa sehari-hari: "Apa status 2026-0042?", "Perangkat mana yang siap diambil?", "Stok apa yang sudah rendah?"
- Asisten dapat memajukan pesanan kerja ke status berikutnya dan menambahkan catatan bengkel; tidak dapat menghapus apa pun, melakukan pembayaran, mengubah harga, atau melihat kata sandi perangkat
- Masuk dengan OAuth 2.1: pemilik bengkel atau teknisi menyetujui pada layar izin, melihat aplikasi yang terhubung, dan dapat memutuskan kapan saja

## Tangkapan Layar

| Panel | Penerimaan perangkat |
|---|---|
| ![Panel](docs/screenshots/panel-en.png) | ![Formulir penerimaan](docs/screenshots/intake-en.png) |

| Pesanan kerja | Halaman pelacakan pelanggan |
|---|---|
| ![Pesanan kerja](docs/screenshots/work-order-en.png) | ![Halaman pelacakan di ponsel](docs/screenshots/tracking-en.png) |

![Halaman depan](docs/screenshots/landing-en.png)

| Menghubungkan asisten AI | Aplikasi yang terhubung |
|---|---|
| ![Layar izin](docs/screenshots/mcp-consent-en.png) | ![Halaman asisten AI](docs/screenshots/mcp-connections-en.png) |

## Arsitektur

Clean Architecture: ketergantungan hanya mengarah ke dalam.

```
Tamirhane.Web  ──►  Tamirhane.Application  ──►  Tamirhane.Domain
       │                      ▲
       └──► Tamirhane.Infrastructure (hanya untuk mendaftarkan layanan di Program.cs)
```

| Proyek | Tugasnya |
|---|---|
| `Tamirhane.Domain` | Entitas dan aturan bisnis: alur status pesanan kerja, total penawaran, stok, garansi, pemeriksaan IMEI. Tidak bergantung pada framework apa pun. |
| `Tamirhane.Application` | Kasus penggunaan (masing-masing satu kelas) dan antarmuka untuk penyimpanan, email, notifikasi browser, jam, dan enkripsi kata sandi. |
| `Tamirhane.Infrastructure` | EF Core + PostgreSQL, ASP.NET Core Identity, email Resend, Web Push, kode QR, Data Protection. |
| `Tamirhane.Web` | Controller dan view MVC, lokalisasi, header keamanan, pembatasan laju. Server MCP dan endpoint OAuth juga berada di sini sebagai lapisan presentasi kedua yang memanggil kasus penggunaan yang sama. |

Aturan ketergantungan diperiksa oleh `Tamirhane.Architecture.Tests`. 748 test dijalankan terhadap PostgreSQL sungguhan (Testcontainers/Docker atau `TEST_DATABASE_URL`).

Singkatnya soal keamanan: CSP ketat dengan Trusted Types, token CSRF pada setiap POST (satu test menyusuri semua endpoint POST), pembatasan laju per IP, cookie `__Host-`, filter tenant pada setiap query, kata sandi di-hash oleh Identity, kunci Data Protection di disk server, kata sandi perangkat terenkripsi, dan koneksi database yang memverifikasi sertifikat TLS.

## Tumpukan Teknologi

- C#, ASP.NET Core MVC, Entity Framework Core, PostgreSQL (Neon)
- ASP.NET Core Identity, Data Protection
- htmx, Motion (animasi), CSS polos
- Resend (email), Web Push (VAPID)
- Model Context Protocol (C# SDK), OpenIddict (OAuth 2.1)
- xUnit, Testcontainers, NetArchTest
- Oracle Cloud (Ubuntu, systemd), Caddy

## Instalasi

Persyaratan: .NET SDK terbaru dan PostgreSQL. Docker bersifat opsional (untuk test integrasi Testcontainers / database lokal berbasis Docker).

```sh
git clone https://github.com/MrcDprm/tamirhane-repair-tracker.git
cd tamirhane-repair-tracker
cp .env.example .env
```

`.env` dibaca pada Development dengan prioritas terendah. Biarkan `DATABASE_URL` tetap dikomentari untuk PostgreSQL lokal; jika diisi, nilainya mengalahkan `ConnectionStrings:Tamirhane`.

### Opsi A: PostgreSQL lokal (disarankan untuk pengembangan)

Gunakan nilai bawaan dari `src/Tamirhane.Web/appsettings.Development.json`:

```json
"ConnectionStrings": { "Tamirhane": "Host=localhost;Port=5432;Database=tamirhane;Username=tamirhane;Password=tamirhane" }
```

Pembantu Windows/Scoop:

```powershell
scoop install postgresql
.\scripts\dev-local.ps1 check
.\scripts\dev-local.ps1 init       # sekali saja
.\scripts\dev-local.ps1 start      # idempoten; run juga melakukan ini
.\scripts\dev-local.ps1 create-db
.\scripts\dev-local.ps1 migrate
.\scripts\dev-local.ps1 run        # menyalakan PostgreSQL otomatis jika mati
```

Perintah manual:

```sh
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/Tamirhane.Infrastructure --startup-project src/Tamirhane.Infrastructure
dotnet run --project src/Tamirhane.Web/Tamirhane.Web.csproj
```

### Opsi B: PostgreSQL Docker

```sh
docker run -d --name tamirhane-db -e POSTGRES_USER=tamirhane -e POSTGRES_PASSWORD=tamirhane -e POSTGRES_DB=tamirhane -p 5433:5432 postgres:17-alpine
```

Isi `DATABASE_URL=postgresql://tamirhane:tamirhane@localhost:5433/tamirhane` di `.env` (atau gunakan connection string yang sesuai), lalu jalankan migrasi dan aplikasi.

### Opsi C: Neon PostgreSQL

Simpan kredensial Neon di `.env.neon` yang diabaikan Git (atau di variabel lingkungan sungguhan); jangan salin ke `.env`. URL `postgresql://`/`postgres://` didukung; `sslmode=require` diterima.

```powershell
# .env.neon
# DATABASE_URL=postgresql://<user>:<password>@<host>/<db>?sslmode=require
$env:DOTENV_FILE="neon"
dotnet ef database update --project src/Tamirhane.Infrastructure --startup-project src/Tamirhane.Infrastructure
dotnet run --project src/Tamirhane.Web/Tamirhane.Web.csproj
```

Tanpa `DOTENV_FILE`, Development tetap membaca `.env`. Variabel lingkungan sungguhan tetap menang atas kedua file tersebut.

Buka https://localhost:7071 (atau URL yang ditampilkan `dotnet run`) lalu tekan "Coba demo", atau daftarkan bengkel (tautan verifikasi muncul di konsol bila `RESEND_API_KEY` kosong).

Test: `dotnet test`. Test integrasi berbasis Docker/Testcontainers juga dapat dijalankan terhadap PostgreSQL eksternal dengan menetapkan `TEST_DATABASE_URL` atau `DATABASE_URL` (fixture test membuat dan menghapus skema terisolasi untuk setiap proses).

Panduan deployment ke server Linux di belakang Caddy: [deploy/README.md](deploy/README.md). Detail pengembangan lokal: [docs/local-dev.md](docs/local-dev.md).

## Penggunaan dengan Claude

1. Di Claude, buka Settings → Connectors → Add custom connector.
2. Server URL: `https://tamirhane.miracdeprem.com/mcp` (untuk di lokal: `https://localhost:7071/mcp`).
3. Masuk pada halaman Tamirhane yang terbuka lalu tekan Allow. Aplikasi yang terhubung tercantum di menu **Asisten AI**.

Alat: `find_work_order`, `list_work_orders`, `low_stock`, `change_status`. Sebelum mengubah status, klien meminta konfirmasi dari Anda.

## Yang Saya Pelajari

- **Clean Architecture sungguhan:** Saya menjaga domain tetap bebas dari EF Core dan ASP.NET, serta membuat test menggagalkan build bila sebuah proyek mereferensikan lapisan yang salah. Aturan bisnis seperti "perangkat dengan sisa tagihan tidak boleh diambil" berada di satu tempat dan diuji tanpa database.
- **Multi-tenant:** Setiap data bengkel difilter oleh query filter global EF Core yang bersumber dari pengguna yang sedang masuk. Saya menulis test yang mencoba membaca data bengkel lain, dan meninjau satu per satu setiap tempat di mana filter sengaja dimatikan.
- **Header keamanan dan CSP:** Saya mempelajari mengapa skrip dan gaya inline harus hilang, bagaimana Trusted Types memblokir `innerHTML` sementara htmx tetap bekerja lewat satu policy, dan bagaimana test yang menyusuri semua endpoint POST mencegah perlindungan CSRF hilang tanpa terasa.
- **Pembatasan laju di belakang proxy:** Di Render, hop terakhir `X-Forwarded-For` adalah proxy Render sendiri, sehingga semua pengunjung berbagi satu batas; pemindai tautan email mengunci pengguna sungguhan. Saya pindah ke server sendiri di belakang Caddy dan hanya mempercayai header bila berasal dari localhost.
- **Pertimbangan hosting:** Paket gratis yang tidur membuat klik pertama menunggu satu menit. Build mandiri ReadyToRun sebagai layanan systemd yang diperkeras di VM ARM gratis ternyata lebih cepat dan lebih murah, tetapi saya juga belajar bahwa saya sendiri yang harus memperbarui runtime-nya.
- **Menyimpan data terenkripsi:** Kata sandi perangkat dienkripsi dengan ASP.NET Data Protection dan kuncinya berada di disk server, bukan di database, jadi kebocoran database saja tidak membuka apa pun.
- **Lokalisasi dan SEO:** Memindahkan prefiks bahasa ke `PathBase` membuat setiap tautan yang dihasilkan otomatis memuat `/en`. Cookie `__Host-` lalu memerlukan `Path=/`, jika tidak browser akan membuangnya; klien test tidak menerapkan aturan ini, jadi saya menambahkan test yang memeriksanya.
- **Memasukkan asisten AI dengan aman:** Saya membangun server otorisasi OAuth 2.1 dengan OpenIddict yang mengikuti apa yang diharapkan klien MCP: dokumen discovery, registrasi klien dinamis yang hanya mengizinkan alamat redirect dikenal, PKCE, dan resource indicator. Token hanya menyebut pengguna; bengkel, peran, dan cabang dibaca dari database pada setiap permintaan, sehingga mencabut staf atau memutus koneksi berlaku pada panggilan berikutnya. Teks yang ditulis pelanggan dikembalikan sebagai data di kolom tersendiri agar tidak sampai menjadi instruksi bagi asisten.
- **Detail produk:** Formulir penerimaan menyesua jenis perangkat, tampilan ponsel diperiksa pada 320–768 piksel dalam bahasa Indonesia dan English, dan notifikasi iPhone memerlukan manifest web app tersendiri untuk setiap halaman pelacakan.

## Lisensi

[AGPL-3.0](LICENSE). Jika perusahaan Anda tidak dapat memenuhi ketentuan AGPL (misalnya ingin mengubah kode tanpa mempublikasikan perubahan), lisensi komersial dimungkinkan: [hubungi kami](https://www.miracdeprem.com/#contact).
