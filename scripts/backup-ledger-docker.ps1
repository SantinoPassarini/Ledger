#requires -Version 5.1
<#
.SYNOPSIS
Creates a verified Ledger backup from a machine that only has Docker.

.DESCRIPTION
Runs pg_dump and pg_restore inside the official postgres:17-alpine image, so the host
does not need PostgreSQL client tools. The backup is written to the host output
directory through a bind mount. The connection string is passed to the container as
an environment variable, never as a command-line argument.

Accepts a libpq URI (postgresql://...) or an Npgsql-style connection string
(Host=...;Database=...;Username=...;Password=...;SSL Mode=Require).

.EXAMPLE
$env:LEDGER_BACKUP_CONNECTION = 'postgresql://user:password@host/db?sslmode=require'
.\scripts\backup-ledger-docker.ps1

.EXAMPLE
.\scripts\backup-ledger-docker.ps1 -OutputDirectory D:\Backups\Ledger -RetentionDays 14
#>
[CmdletBinding()]
param(
    [string]$ConnectionString = $env:LEDGER_BACKUP_CONNECTION,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\backups"),
    [ValidateRange(1, 3650)]
    [int]$RetentionDays = 30,
    [string]$Image = "postgres:17-alpine",
    [switch]$SkipVerification,
    [switch]$KeepOnFailure
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function ConvertTo-LedgerBackupEnvironment {
    param([Parameter(Mandatory)][string]$Value)

    $environment = @{}

    if ($Value -match '^\s*postgres(ql)?://') {
        # libpq understands the URI directly and performs its own percent-decoding.
        $environment["BACKUP_DB"] = $Value.Trim()
        return $environment
    }

    $builder = [System.Data.Common.DbConnectionStringBuilder]::new()

    try {
        # PowerShell 7.6 binds "ConnectionString = value" as a legacy property setter
        # that treats the whole value as one key. The .NET setter must be called explicitly.
        $builder.set_ConnectionString($Value)
    } catch {
        throw "The connection string could not be parsed: $($_.Exception.Message)"
    }

    foreach ($key in $builder.Keys) {
        $normalized = $key.ToString().Trim().ToLowerInvariant()
        $value = [string]$builder[$key]

        switch ($normalized) {
            "host" { $environment["PGHOST"] = $value }
            "server" { $environment["PGHOST"] = $value }
            "hostaddr" { $environment["PGHOST"] = $value }
            "port" { $environment["PGPORT"] = $value }
            "database" { $environment["PGDATABASE"] = $value }
            "dbname" { $environment["PGDATABASE"] = $value }
            "initial catalog" { $environment["PGDATABASE"] = $value }
            "username" { $environment["PGUSER"] = $value }
            "user id" { $environment["PGUSER"] = $value }
            "user" { $environment["PGUSER"] = $value }
            "password" { $environment["PGPASSWORD"] = $value }
            "pwd" { $environment["PGPASSWORD"] = $value }
            "ssl mode" { $environment["PGSSLMODE"] = $value.ToLowerInvariant() }
            "sslmode" { $environment["PGSSLMODE"] = $value.ToLowerInvariant() }
        }
    }

    if ([string]::IsNullOrWhiteSpace($environment["PGDATABASE"])) {
        throw "The connection string does not contain a database name."
    }

    if ([string]::IsNullOrWhiteSpace($environment["PGUSER"])) {
        throw "The connection string does not contain a user name."
    }

    $environment["BACKUP_DB"] = $environment["PGDATABASE"]
    return $environment
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    throw "Set LEDGER_BACKUP_CONNECTION or pass -ConnectionString. " +
          "Do not commit the connection string to Git."
}

Get-Command docker -ErrorAction Stop | Out-Null

$environment = ConvertTo-LedgerBackupEnvironment -Value $ConnectionString
$output = [System.IO.Path]::GetFullPath($OutputDirectory)
$timestamp = [DateTime]::UtcNow.ToString("yyyyMMdd-HHmmss")
$fileName = "ledger-$timestamp.dump"
$hostFile = Join-Path $output $fileName
$containerFile = "/backup/$fileName"

New-Item -ItemType Directory -Path $output -Force | Out-Null

$command = @(
    'pg_dump'
    '--format=custom'
    '--no-owner'
    '--no-privileges'
    '--file "$BACKUP_FILE"'
    '--dbname "$BACKUP_DB"'
) -join ' '

if (-not $SkipVerification) {
    $command += ' && pg_restore --list "$BACKUP_FILE" > /dev/null'
}

$dockerArguments = @(
    'run'
    '--rm'
    '--env'
    "BACKUP_FILE=$containerFile"
)

# Pass secrets to the container through the process environment, then reference them
# by name only. Values (password, URI) never appear in the Docker command line.
$restore = @{}

foreach ($key in ($environment.Keys | Sort-Object)) {
    $previous = [System.Environment]::GetEnvironmentVariable($key, 'Process')
    $restore[$key] = $previous
    [System.Environment]::SetEnvironmentVariable($key, $environment[$key], 'Process')
    $dockerArguments += @('--env', $key)
}

$dockerArguments += @(
    '--mount'
    "type=bind,source=$output,target=/backup"
    $Image
    'sh'
    '-c'
    $command
)

try {
    Write-Host "Dumping database to $hostFile ..."
    & docker @dockerArguments

    if ($LASTEXITCODE -ne 0) {
        throw "The Docker backup command failed with exit code $LASTEXITCODE."
    }
} catch {
    if (-not $KeepOnFailure -and (Test-Path -LiteralPath $hostFile)) {
        Remove-Item -LiteralPath $hostFile -Force -ErrorAction SilentlyContinue
    }

    throw
} finally {
    foreach ($key in $restore.Keys) {
        [System.Environment]::SetEnvironmentVariable($key, $restore[$key], 'Process')
    }

    $environment.Clear()
    $restore.Clear()
}

$backup = Get-Item -LiteralPath $hostFile

if ($backup.Length -le 0) {
    Remove-Item -LiteralPath $hostFile -Force
    throw "pg_dump produced an empty file."
}

$cutoff = [DateTime]::UtcNow.AddDays(-$RetentionDays)

Get-ChildItem -LiteralPath $output -File -Filter "ledger-*.dump" |
    Where-Object { $_.LastWriteTimeUtc -lt $cutoff } |
    ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Force
        Write-Host "Removed expired backup $($_.Name)."
    }

$sizeMb = [Math]::Round($backup.Length / 1MB, 2)
Write-Host "Backup completed: $hostFile ($sizeMb MB)."
Write-Host "Copy it off-server and keep it outside this repository."
