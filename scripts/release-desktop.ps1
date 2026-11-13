#requires -Version 5.1
<#
.SYNOPSIS
Builds the distributable Ledger.Desktop package for Windows x64.

.DESCRIPTION
Publishes a self-contained, single-file WinForms executable, writes the API base URL
into an external appsettings.json, adds the end-user README, creates a ZIP and writes
SHA256 checksums. The client does not need to be recompiled to change the API URL:
edit appsettings.json next to the executable or set LEDGER_API_BASE_URL.

.EXAMPLE
.\scripts\release-desktop.ps1 -Version 1.0.0 -ApiBaseUrl https://ledger-api.onrender.com/
#>
[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [string]$ApiBaseUrl = "http://localhost:5100/",
    [string]$OutputRoot = (Join-Path $PSScriptRoot "..\artifacts\release"),
    [switch]$SkipBuild,
    [switch]$SkipArchive
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$') {
    throw "Version must look like 1.0.0 or 1.0.0-beta.1."
}

if (-not [Uri]::TryCreate($ApiBaseUrl, [UriKind]::Absolute, [ref]$null)) {
    throw "ApiBaseUrl must be an absolute URL, for example https://ledger-api.onrender.com/."
}

$apiUri = [Uri]$ApiBaseUrl

if ($apiUri.Scheme -notin @("http", "https")) {
    throw "ApiBaseUrl must use http or https."
}

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$project = Join-Path $repoRoot "src\Ledger.Desktop\Ledger.Desktop.csproj"
$template = Join-Path $repoRoot "docs\onboarding\README-desktop.txt"
$outputRootFull = [IO.Path]::GetFullPath($OutputRoot)
$packageName = "Ledger.Desktop-$Version-win-x64"
$packageDir = Join-Path $outputRootFull $packageName
$zipPath = Join-Path $outputRootFull "$packageName.zip"
$checksumsPath = Join-Path $outputRootFull "$packageName.sha256.txt"

if (-not (Test-Path -LiteralPath $template)) {
    throw "The end-user README template was not found: $template"
}

New-Item -ItemType Directory -Path $outputRootFull -Force | Out-Null

if (Test-Path -LiteralPath $packageDir) {
    Remove-Item -LiteralPath $packageDir -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

if (Test-Path -LiteralPath $checksumsPath) {
    Remove-Item -LiteralPath $checksumsPath -Force
}

New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

if (-not $SkipBuild) {
    Write-Host "Publishing Ledger.Desktop $Version for win-x64 ..."

    & dotnet publish $project `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -p:Version=$Version `
        --output $packageDir

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }
}

Get-ChildItem -LiteralPath $packageDir -File -Filter "*.pdb" |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }

$settingsPath = Join-Path $packageDir "appsettings.json"
$settings = [ordered]@{ ApiBaseUrl = $ApiBaseUrl }
$settingsJson = ($settings | ConvertTo-Json) + [Environment]::NewLine
[IO.File]::WriteAllText($settingsPath, $settingsJson, [Text.UTF8Encoding]::new($false))

$generatedAt = [DateTime]::UtcNow.ToString("yyyy-MM-dd")
$readme = Get-Content -LiteralPath $template -Raw
$readme = $readme.Replace("{{VERSION}}", $Version)
$readme = $readme.Replace("{{API_BASE_URL}}", $ApiBaseUrl)
$readme = $readme.Replace("{{GENERATED_AT}}", $generatedAt)
[IO.File]::WriteAllText(
    (Join-Path $packageDir "README.txt"),
    $readme,
    [Text.UTF8Encoding]::new($false))

$exePath = Join-Path $packageDir "Ledger.Desktop.exe"

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "The published executable was not found: $exePath"
}

$exeHash = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash

if (-not $SkipArchive) {
    Write-Host "Creating $zipPath ..."
    Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $zipPath -CompressionLevel Optimal
}

$lines = @(
    "Ledger.Desktop $Version - win-x64",
    "Generated (UTC): $([DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm:ss'))",
    "API base URL: $ApiBaseUrl",
    "",
    "SHA256  Ledger.Desktop.exe",
    "$exeHash  Ledger.Desktop.exe"
)

if (Test-Path -LiteralPath $zipPath) {
    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    $lines += @("", "SHA256  $packageName.zip", "$zipHash  $packageName.zip")
}

[IO.File]::WriteAllLines($checksumsPath, $lines, [Text.UTF8Encoding]::new($false))

$totalMb = [Math]::Round(
    ((Get-ChildItem -LiteralPath $packageDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB),
    2)

Write-Host ""
Write-Host "Release ready."
Write-Host "  Directory: $packageDir ($totalMb MB)"
if (Test-Path -LiteralPath $zipPath) {
    Write-Host "  ZIP:       $zipPath"
}
Write-Host "  Checksums: $checksumsPath"
Write-Host "  EXE SHA256: $exeHash"
