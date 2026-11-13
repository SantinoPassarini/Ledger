param(
    [string]$ApiBaseUrl = 'http://localhost:5100/',
    [string]$Identifier = $env:LEDGER_SMOKE_IDENTIFIER,
    [string]$Password = $env:LEDGER_SMOKE_PASSWORD,
    [switch]$Screenshots
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Identifier) -or [string]::IsNullOrWhiteSpace($Password)) {
    Write-Host 'Credentials are required. Pass -Identifier and -Password, or set'
    Write-Host 'LEDGER_SMOKE_IDENTIFIER and LEDGER_SMOKE_PASSWORD.'
    Write-Host 'The account must be provisioned in the API through user secrets or environment variables.'
    exit 1
}

$testConnection = $env:LEDGER_TEST_CONNECTION

if ([string]::IsNullOrWhiteSpace($testConnection)) {
    Write-Host 'LEDGER_TEST_CONNECTION is required. The desktop smoke test must run against'
    Write-Host 'a disposable PostgreSQL database, never against Neon or production data.'
    exit 1
}

if ($testConnection -match 'neon\.tech') {
    Write-Host 'LEDGER_TEST_CONNECTION points to Neon. Use a disposable local PostgreSQL'
    Write-Host 'database instead; the smoke test creates and archives records.'
    exit 1
}

$env:ConnectionStrings__Ledger = $testConnection

# A production JWT key from user secrets must not be reused for a local smoke run.
$env:Jwt__SigningKey = [Convert]::ToBase64String(
    [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repoRoot 'artifacts\smoke'
$apiDll = Join-Path $repoRoot 'src\Ledger.Api\bin\Debug\net8.0\Ledger.Api.dll'
$stdout = Join-Path $artifacts 'api-stdout.log'
$stderr = Join-Path $artifacts 'api-stderr.log'

New-Item -ItemType Directory -Path $artifacts -Force | Out-Null

dotnet build (Join-Path $repoRoot 'Ledger.sln') --nologo
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = $ApiBaseUrl.TrimEnd('/')

# The API reads Ledger:Owner, Ledger:Users and Jwt:SigningKey from user secrets or the
# current environment. ConnectionStrings:Ledger is overridden above so this script can
# never touch Neon or production data; the signing key is also replaced for the run.
$env:LEDGER_SMOKE_IDENTIFIER = $Identifier
$env:LEDGER_SMOKE_PASSWORD = $Password

$api = Start-Process -FilePath dotnet `
    -ArgumentList $apiDll `
    -PassThru `
    -NoNewWindow `
    -RedirectStandardOutput $stdout `
    -RedirectStandardError $stderr

try {
    $healthy = $false

    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        Start-Sleep -Seconds 1

        try {
            $health = Invoke-WebRequest -Uri "$($ApiBaseUrl.TrimEnd('/'))/health" -UseBasicParsing -TimeoutSec 5
            if ($health.StatusCode -eq 200) {
                $healthy = $true
                break
            }
        }
        catch {
        }
    }

    if (-not $healthy) {
        Write-Host 'API did not become healthy. See artifacts/smoke/api-stderr.log.'
        exit 1
    }

    $arguments = @(
        'run',
        '--project',
        (Join-Path $repoRoot 'tests\Ledger.Desktop.Smoke\Ledger.Desktop.Smoke.csproj'),
        '--no-build',
        '--',
        '--api',
        $ApiBaseUrl
    )

    if ($Screenshots) {
        $arguments += '--screenshots'
    }

    & dotnet @arguments
    $code = $LASTEXITCODE
}
finally {
    Stop-Process -Id $api.Id -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}

exit $code
