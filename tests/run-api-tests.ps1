param(
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'tests\Ledger.Api.Tests\Ledger.Api.Tests.csproj'
$testConnection = $env:LEDGER_TEST_CONNECTION

if ([string]::IsNullOrWhiteSpace($testConnection)) {
    Write-Host 'LEDGER_TEST_CONNECTION is required.'
    Write-Host 'Start a disposable PostgreSQL instance and set the connection string before'
    Write-Host 'running the tests. The tests create and drop dedicated schemas.'
    Write-Host ''
    Write-Host 'Example with Docker:'
    Write-Host '  docker run --rm -d --name ledger-test -p 5432:5432 \'
    Write-Host '    -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=ledger_test postgres:17-alpine'
    Write-Host '  $env:LEDGER_TEST_CONNECTION = ''Host=localhost;Port=5432;Database=ledger_test;Username=postgres;Password=postgres'''
    exit 1
}

if ($testConnection -match 'neon\.tech') {
    Write-Host 'LEDGER_TEST_CONNECTION points to Neon. API tests create and drop schemas,'
    Write-Host 'so they must run against a disposable local PostgreSQL instance.'
    exit 1
}

Write-Host 'Running Ledger API tests. A unique PostgreSQL schema will be created and dropped.'
Write-Host "Connection source: LEDGER_TEST_CONNECTION ($Configuration)."

dotnet test $project --configuration $Configuration --nologo
exit $LASTEXITCODE
