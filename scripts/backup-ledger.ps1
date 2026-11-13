<#
.SYNOPSIS
Creates a verified, self-contained backup of the Ledger PostgreSQL database.

.DESCRIPTION
Uses pg_dump in custom format, verifies the archive with pg_restore --list and applies
a retention policy to old backups in the output directory. The script is independent
from the Ledger API: it never calls the API and never exposes a backup endpoint.

The connection string can be a PostgreSQL URI, a libpq keyword string, or an Npgsql
connection string. The password is passed to pg_dump through PGPASSWORD instead of the
command line.

.EXAMPLE
$env:LEDGER_BACKUP_CONNECTION = 'postgresql://user:password@host/db?sslmode=require'
.\scripts\backup-ledger.ps1

.EXAMPLE
.\scripts\backup-ledger.ps1 -OutputDirectory D:\Backups\Ledger -RetentionDays 14
#>
[CmdletBinding()]
param(
    [string]$ConnectionString = $env:LEDGER_BACKUP_CONNECTION,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\backups"),
    [ValidateRange(1, 3650)]
    [int]$RetentionDays = 30,
    [string]$PgDump = $(if ($env:PG_DUMP) { $env:PG_DUMP } else { "pg_dump" }),
    [string]$PgRestore = $(if ($env:PG_RESTORE) { $env:PG_RESTORE } else { "pg_restore" }),
    [switch]$SkipVerification,
    [switch]$KeepOnFailure
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function ConvertFrom-LedgerConnectionString {
    param([Parameter(Mandatory)][string]$Value)

    $settings = [ordered]@{
        Server = $null
        Port = $null
        User = $null
        Password = $null
        Database = $null
        SslMode = $null
    }

    if ($Value -match '^\s*postgres(ql)?://') {
        $uri = [System.Uri]::new($Value)
        $settings.Server = $uri.Host

        if (-not $uri.IsDefaultPort) {
            $settings.Port = [string]$uri.Port
        }

        if (-not [string]::IsNullOrWhiteSpace($uri.UserInfo)) {
            $userInfo = $uri.UserInfo.Split(':', 2)
            $settings.User = [System.Uri]::UnescapeDataString($userInfo[0])

            if ($userInfo.Length -gt 1) {
                $settings.Password = [System.Uri]::UnescapeDataString($userInfo[1])
            }
        }

        if (-not [string]::IsNullOrWhiteSpace($uri.AbsolutePath)) {
            $settings.Database = [System.Uri]::UnescapeDataString(
                $uri.AbsolutePath.TrimStart('/'))
        }

        foreach ($pair in $uri.Query.TrimStart('?').Split(
            '&',
            [System.StringSplitOptions]::RemoveEmptyEntries)) {
            $keyValue = $pair.Split('=', 2)
            $key = [System.Uri]::UnescapeDataString($keyValue[0]).ToLowerInvariant()
            $queryValue = if ($keyValue.Length -gt 1) {
                [System.Uri]::UnescapeDataString($keyValue[1])
            } else {
                ""
            }

            switch ($key) {
                "sslmode" { $settings.SslMode = $queryValue.ToLowerInvariant() }
                "host" { $settings.Server = $queryValue }
                "port" { $settings.Port = $queryValue }
                "user" { $settings.User = $queryValue }
                "password" { $settings.Password = $queryValue }
                "dbname" { $settings.Database = $queryValue }
            }
        }
    } else {
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
                "host" { $settings.Server = $value }
                "server" { $settings.Server = $value }
                "hostaddr" { $settings.Server = $value }
                "port" { $settings.Port = $value }
                "database" { $settings.Database = $value }
                "dbname" { $settings.Database = $value }
                "initial catalog" { $settings.Database = $value }
                "username" { $settings.User = $value }
                "user id" { $settings.User = $value }
                "user" { $settings.User = $value }
                "password" { $settings.Password = $value }
                "pwd" { $settings.Password = $value }
                "ssl mode" { $settings.SslMode = $value.ToLowerInvariant() }
                "sslmode" { $settings.SslMode = $value.ToLowerInvariant() }
            }
        }
    }

    if ([string]::IsNullOrWhiteSpace($settings.Database)) {
        throw "The connection string does not contain a database name."
    }

    if ([string]::IsNullOrWhiteSpace($settings.User)) {
        throw "The connection string does not contain a user name."
    }

    return [pscustomobject]$settings
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    throw "Set LEDGER_BACKUP_CONNECTION or pass -ConnectionString. " +
          "Do not commit the connection string to Git."
}

$settings = ConvertFrom-LedgerConnectionString -Value $ConnectionString
$output = [System.IO.Path]::GetFullPath($OutputDirectory)
$timestamp = [DateTime]::UtcNow.ToString("yyyyMMdd-HHmmss")
$file = Join-Path $output "ledger-$timestamp.dump"

New-Item -ItemType Directory -Path $output -Force | Out-Null

Get-Command $PgDump -ErrorAction Stop | Out-Null

if (-not $SkipVerification) {
    Get-Command $PgRestore -ErrorAction Stop | Out-Null
}

$arguments = @(
    "--format=custom",
    "--no-owner",
    "--no-privileges",
    "--file", $file,
    "--dbname", $settings.Database
)

if (-not [string]::IsNullOrWhiteSpace($settings.Server)) {
    $arguments += @("--host", $settings.Server)
}

if (-not [string]::IsNullOrWhiteSpace($settings.Port)) {
    $arguments += @("--port", $settings.Port)
}

if (-not [string]::IsNullOrWhiteSpace($settings.User)) {
    $arguments += @("--username", $settings.User)
}

$previousPassword = $env:PGPASSWORD
$previousSslMode = $env:PGSSLMODE

try {
    if (-not [string]::IsNullOrWhiteSpace($settings.Password)) {
        $env:PGPASSWORD = $settings.Password
    }

    if (-not [string]::IsNullOrWhiteSpace($settings.SslMode)) {
        $env:PGSSLMODE = $settings.SslMode
    }

    Write-Host "Dumping database '$($settings.Database)' to $file ..."

    & $PgDump @arguments

    if ($LASTEXITCODE -ne 0) {
        throw "pg_dump failed with exit code $LASTEXITCODE."
    }

    if (-not $SkipVerification) {
        & $PgRestore "--list" $file | Out-Null

        if ($LASTEXITCODE -ne 0) {
            throw "pg_restore --list failed with exit code $LASTEXITCODE. " +
                  "The backup archive is not readable."
        }
    }
} catch {
    if (-not $KeepOnFailure -and (Test-Path -LiteralPath $file)) {
        Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue
    }

    throw
} finally {
    $env:PGPASSWORD = $previousPassword
    $env:PGSSLMODE = $previousSslMode
}

$backup = Get-Item -LiteralPath $file

if ($backup.Length -le 0) {
    Remove-Item -LiteralPath $file -Force
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
Write-Host "Backup completed: $file ($sizeMb MB)."
Write-Host "Copy it off-server and keep it outside this repository."
