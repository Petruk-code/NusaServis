# Pengembangan PostgreSQL lokal (Scoop, Windows)

Repo ini dikonfigurasi untuk PostgreSQL lokal di `src/Tamirhane.Web/appsettings.Development.json`:

```json
"ConnectionStrings": { "Tamirhane": "Host=localhost;Port=5432;Database=tamirhane;Username=tamirhane;Password=tamirhane" }
```

`Program.cs` memuat `.env` pada Development dengan prioritas terendah. Biarkan `DATABASE_URL` tetap dikomentari di `.env` untuk pengembangan lokal; jika diisi, nilainya mengalahkan connection string lokal.

## Prasyarat

```powershell
scoop install postgresql
dotnet --info
dotnet tool install --global dotnet-ef   # atau: dotnet tool update --global dotnet-ef
```

Jika `dotnet ef` tidak menemukan .NET, buka shell baru atau set:

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\scoop\apps\dotnet-sdk\current"
$env:Path="$env:DOTNET_ROOT;$env:USERPROFILE\.dotnet\tools;$env:Path"
```

## Mulai cepat

```powershell
.\scripts\dev-local.ps1 check      # cek dotnet/postgres
.\scripts\dev-local.ps1 init       # initdb sekali saja (trust auth, hanya lokal)
.\scripts\dev-local.ps1 start      # menyalakan postgres (idempoten; run juga melakukan ini)
.\scripts\dev-local.ps1 create-db  # membuat user tamirhane serta DB tamirhane/tamirhane_test jika belum ada
.\scripts\dev-local.ps1 migrate    # dotnet ef database update
.\scripts\dev-local.ps1 run        # dotnet run web; menyalakan PostgreSQL otomatis jika mati
```

Jalankan test integrasi terhadap database test yang terpisah:

```powershell
$env:TEST_DATABASE_URL="Host=localhost;Port=5432;Database=tamirhane_test;Username=tamirhane;Password=tamirhane"
dotnet test
```

Hentikan PostgreSQL dengan:

```powershell
.\scripts\dev-local.ps1 stop
```

## Neon kemudian

Simpan kredensial Neon di `.env.neon` yang diabaikan Git; jangan salin ke `.env`. Untuk memakai file itu pada aplikasi, set `DOTENV_FILE=neon` di shell saat ini:

```powershell
$env:DOTENV_FILE="neon"
dotnet ef database update --project src/Tamirhane.Infrastructure --startup-project src/Tamirhane.Infrastructure
dotnet run --project src/Tamirhane.Web/Tamirhane.Web.csproj
```

Gunakan perintah di atas langsung agar pembantu PostgreSQL lokal tidak ikut berjalan.

Tanpa `DOTENV_FILE`, Development tetap membaca `.env`. Variabel lingkungan sungguhan tetap menang, dan test memakai `DOTENV=off` plus `TEST_DATABASE_URL`.
