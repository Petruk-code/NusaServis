# Local PostgreSQL dev (Scoop, Windows)

This repo is configured for local PostgreSQL in `src/Tamirhane.Web/appsettings.Development.json`:

```json
"ConnectionStrings": { "Tamirhane": "Host=localhost;Port=5432;Database=tamirhane;Username=tamirhane;Password=tamirhane" }
```

`Program.cs` loads `.env` in Development with lowest priority. Keep `DATABASE_URL` commented in `.env` for local dev; otherwise it overrides the local connection string.

## Prerequisites

```powershell
scoop install postgresql
dotnet --info
dotnet tool install --global dotnet-ef   # or: dotnet tool update --global dotnet-ef
```

If `dotnet ef` cannot find .NET, open a new shell or set:

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\scoop\apps\dotnet-sdk\current"
$env:Path="$env:DOTNET_ROOT;$env:USERPROFILE\.dotnet\tools;$env:Path"
```

## Quick start

```powershell
.\scripts\dev-local.ps1 check      # verify dotnet/postgres tools
.\scripts\dev-local.ps1 init       # one-time initdb (trust auth, local dev only)
.\scripts\dev-local.ps1 start      # start postgres (idempotent; run also does this)
.\scripts\dev-local.ps1 create-db  # create tamirhane user plus tamirhane/tamirhane_test DBs if missing
.\scripts\dev-local.ps1 migrate    # dotnet ef database update
.\scripts\dev-local.ps1 run        # dotnet run web; auto-starts PostgreSQL if stopped
```

Run integration tests against the separate test database:

```powershell
$env:TEST_DATABASE_URL="Host=localhost;Port=5432;Database=tamirhane_test;Username=tamirhane;Password=tamirhane"
dotnet test
```

Stop PostgreSQL with:

```powershell
.\scripts\dev-local.ps1 stop
```

## Neon later

Keep Neon credentials in the ignored `.env.neon`; do not copy them into `.env`. To use that file for the app, set `DOTENV_FILE=neon` for the current shell:

```powershell
$env:DOTENV_FILE="neon"
dotnet ef database update --project src/Tamirhane.Infrastructure --startup-project src/Tamirhane.Infrastructure
dotnet run --project src/Tamirhane.Web/Tamirhane.Web.csproj
```

Use these directly so the local PostgreSQL helper is not started.

Without `DOTENV_FILE`, Development keeps reading `.env`. Real environment variables still win, and tests use `DOTENV=off` plus `TEST_DATABASE_URL`.
