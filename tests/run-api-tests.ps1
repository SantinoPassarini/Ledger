param(
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'tests\Ledger.Api.Tests\Ledger.Api.Tests.csproj'

Write-Host 'Running Ledger API tests. A unique PostgreSQL schema will be created and dropped.'
Write-Host 'Connection source: LEDGER_TEST_CONNECTION or Ledger.Api user secrets.'

dotnet test $project --configuration $Configuration --nologo
exit $LASTEXITCODE
