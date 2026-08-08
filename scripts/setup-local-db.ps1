# Creates the scorecard role and database on a local PostgreSQL install.
#
# Mirrors what docker/docker-compose.yml provisions, so a developer on native
# Postgres and one on Docker end up with the same credentials and the same
# connection string. Safe to re-run.

param(
    [string]$SuperUser = "postgres",
    [string]$SuperPassword = "postgres",
    [string]$AppUser = "scorecard",
    [string]$AppPassword = "scorecard",
    [string]$Database = "scorecard",
    [int]$Port = 5432
)

$ErrorActionPreference = "Stop"

function Resolve-Psql {
    $cmd = Get-Command psql -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    # The EDB installer does not add itself to PATH by default.
    # @(...) forces an array: a single match would otherwise be a string, and
    # indexing a string returns its first character rather than the path.
    $candidates = @(
        Get-ChildItem "C:\Program Files\PostgreSQL" -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName "bin\psql.exe" } |
            Where-Object { Test-Path $_ }
    )

    if ($candidates.Count -gt 0) { return $candidates[0] }

    throw "psql not found. Install PostgreSQL first, then re-run this script."
}

$psql = Resolve-Psql
Write-Host "Using psql at $psql"

$env:PGPASSWORD = $SuperPassword

# CREATE ROLE / CREATE DATABASE have no IF NOT EXISTS, so guard each one.
$roleExists = & $psql -U $SuperUser -h localhost -p $Port -d postgres -tAc `
    "SELECT 1 FROM pg_roles WHERE rolname = '$AppUser'"

if ($roleExists -eq "1") {
    Write-Host "Role '$AppUser' already exists."
} else {
    & $psql -U $SuperUser -h localhost -p $Port -d postgres -c `
        "CREATE ROLE $AppUser LOGIN PASSWORD '$AppPassword'"
    Write-Host "Created role '$AppUser'."
}

$dbExists = & $psql -U $SuperUser -h localhost -p $Port -d postgres -tAc `
    "SELECT 1 FROM pg_database WHERE datname = '$Database'"

if ($dbExists -eq "1") {
    Write-Host "Database '$Database' already exists."
} else {
    & $psql -U $SuperUser -h localhost -p $Port -d postgres -c `
        "CREATE DATABASE $Database OWNER $AppUser"
    Write-Host "Created database '$Database'."
}

# EF creates the schema, but the app role must be able to.
& $psql -U $SuperUser -h localhost -p $Port -d $Database -c `
    "GRANT ALL ON SCHEMA public TO $AppUser" | Out-Null

$env:PGPASSWORD = $AppPassword
$check = & $psql -U $AppUser -h localhost -p $Port -d $Database -tAc "SELECT version()"
Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Connected as '$AppUser': $check"
Write-Host "Connection string: Host=localhost;Port=$Port;Database=$Database;Username=$AppUser;Password=$AppPassword"
