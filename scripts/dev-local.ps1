[CmdletBinding()]
param(
  [Parameter(Mandatory, Position = 0)]
  [ValidateSet('check','init','start','stop','status','create-db','migrate','run')]
  [string]$Action
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$pgDataCandidates = @(
  $env:PGDATA,
  (Join-Path $env:USERPROFILE 'scoop\apps\postgresql\current\data'),
  (Join-Path $env:USERPROFILE 'scoop\persist\postgresql\data')
) | Where-Object { $_ }
$pgData = $pgDataCandidates | Where-Object { Test-Path (Join-Path $_ 'PG_VERSION') } | Select-Object -First 1
if (-not $pgData) { $pgData = $pgDataCandidates | Select-Object -First 1 }
$pgLog  = Join-Path $env:USERPROFILE 'scoop\persist\postgresql\postgres.log'
$dotnetRoot = Join-Path $env:USERPROFILE 'scoop\apps\dotnet-sdk\current'

if (Test-Path $dotnetRoot) {
  $env:DOTNET_ROOT = $dotnetRoot
  $env:Path = "$dotnetRoot;$env:USERPROFILE\.dotnet\tools;$env:Path"
}

function Require-Cmd([string]$name, [string]$hint) {
  if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
    throw "$name not found. $hint"
  }
}

function Test-PostgresRunning {
  if (-not (Get-Command pg_ctl -ErrorAction SilentlyContinue)) { return $false }
  & pg_ctl -D $pgData status *> $null
  return $LASTEXITCODE -eq 0
}

function Ensure-Postgres {
  Require-Cmd pg_ctl 'Install PostgreSQL: scoop install postgresql'
  if (-not (Test-Path (Join-Path $pgData 'PG_VERSION'))) {
    throw "PostgreSQL data not initialized. Run .\scripts\dev-local.ps1 init first."
  }
  if (Test-PostgresRunning) {
    Write-Host 'PostgreSQL already running.'
    return
  }
  $logDir = Split-Path -Parent $pgLog
  if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Force -Path $logDir | Out-Null }
  Write-Host "Starting PostgreSQL at $pgData"
  pg_ctl -D $pgData -l $pgLog start
}

switch ($Action) {
  'check' {
    Require-Cmd dotnet 'Install .NET SDK or open a shell with DOTNET_ROOT set.'
    $pg = Get-Command psql, pg_ctl, initdb -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
    Write-Host "DOTNET_ROOT=$env:DOTNET_ROOT"
    Write-Host "dotnet=$( (Get-Command dotnet).Source )"
    Write-Host "dotnet-ef=$( (Get-Command dotnet-ef -ErrorAction SilentlyContinue).Source )"
    Write-Host "postgres tools:"; $pg | ForEach-Object { Write-Host "  $_" }
    Write-Host "pgData=$pgData"
  }
  'init' {
    Require-Cmd initdb 'Install PostgreSQL: scoop install postgresql'
    if (Test-Path (Join-Path $pgData 'PG_VERSION')) { Write-Host "Already initialized: $pgData"; break }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $pgData) | Out-Null
    initdb -D $pgData --username=postgres --encoding=UTF8 --auth=trust
  }
  'start' {
    Ensure-Postgres
  }
  'stop' {
    Require-Cmd pg_ctl 'Install PostgreSQL: scoop install postgresql'
    pg_ctl -D $pgData stop -m fast
  }
  'status' {
    Require-Cmd pg_ctl 'Install PostgreSQL: scoop install postgresql'
    pg_ctl -D $pgData status
  }
  'create-db' {
    Ensure-Postgres
    Require-Cmd psql 'Install PostgreSQL: scoop install postgresql'
    $role = (& psql -U postgres -Atc "SELECT 1 FROM pg_roles WHERE rolname='tamirhane'").Trim()
    if ($role -ne '1') {
      psql -U postgres -v ON_ERROR_STOP=1 -c "CREATE USER tamirhane WITH PASSWORD 'tamirhane';" | Out-Host
    }
    $db = (& psql -U postgres -Atc "SELECT 1 FROM pg_database WHERE datname='tamirhane'").Trim()
    if ($db -ne '1') {
      psql -U postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE tamirhane OWNER tamirhane;" | Out-Host
    }
    $testDb = (& psql -U postgres -Atc "SELECT 1 FROM pg_database WHERE datname='tamirhane_test'").Trim()
    if ($testDb -ne '1') {
      psql -U postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE tamirhane_test OWNER tamirhane;" | Out-Host
    }
    Write-Host "Local DB ready: Host=localhost;Port=5432;Database=tamirhane;Username=tamirhane;Password=tamirhane"
    Write-Host "Test DB ready: Host=localhost;Port=5432;Database=tamirhane_test;Username=tamirhane;Password=tamirhane"
  }
  'migrate' {
    Ensure-Postgres
    Require-Cmd dotnet 'Install .NET SDK or open a shell with DOTNET_ROOT set.'
    Push-Location $repo
    try {
      dotnet ef database update --project src/Tamirhane.Infrastructure --startup-project src/Tamirhane.Infrastructure
    }
    finally { Pop-Location }
  }
  'run' {
    Ensure-Postgres
    Require-Cmd dotnet 'Install .NET SDK or open a shell with DOTNET_ROOT set.'
    Push-Location $repo
    try {
      dotnet run --project src/Tamirhane.Web/Tamirhane.Web.csproj
    }
    finally { Pop-Location }
  }
}
