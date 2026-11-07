param(
    [string]$ApiBaseUrl = 'http://localhost:5100/',
    [switch]$Screenshots
)

$ErrorActionPreference = 'Stop'

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
