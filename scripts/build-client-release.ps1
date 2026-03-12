param(
    [string]$Version = "1.1.0",
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputRoot = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "artifacts\\client"
}

$assemblyVersion = if ($Version.Split('.').Count -ge 4) { $Version } else { "$Version.0" }
$publishRoot = Join-Path $OutputRoot ("publish\\" + $Version + "\\" + $Runtime)
$packageRoot = Join-Path $OutputRoot ("package\\" + $Version)
$zipPath = Join-Path $packageRoot ("SecureLicensePlatform-Client-" + $Version + "-" + $Runtime + ".zip")
$projectPath = Join-Path $repoRoot "src\\Platform.Client.Wpf\\Platform.Client.Wpf.csproj"
$publicKeySource = Join-Path $repoRoot "deploy\\keys\\update-public.pem"
$sampleSettingsSource = Join-Path $repoRoot "deploy\\examples\\clientsettings.sample.json"

if (!(Test-Path $publicKeySource)) {
    throw "Update signing public key was not found: $publicKeySource"
}

if (Test-Path $publishRoot) {
    Remove-Item $publishRoot -Recurse -Force
}

if (Test-Path $packageRoot) {
    Remove-Item $packageRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null

dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:Version=$Version `
    -p:FileVersion=$assemblyVersion `
    -p:AssemblyVersion=$assemblyVersion `
    -o $publishRoot

$keysTarget = Join-Path $publishRoot "keys"
New-Item -ItemType Directory -Path $keysTarget -Force | Out-Null
Copy-Item $publicKeySource (Join-Path $keysTarget "update-public.pem") -Force
Copy-Item $sampleSettingsSource (Join-Path $publishRoot "clientsettings.sample.json") -Force

Compress-Archive -Path (Join-Path $publishRoot "*") -DestinationPath $zipPath -Force

Write-Host ""
Write-Host "Portable client build is ready."
Write-Host "Publish folder: $publishRoot"
Write-Host "ZIP package:    $zipPath"
Write-Host ""
Write-Host "Use these values in the admin release form:"
Write-Host "Version: $Version"
Write-Host "File:    $zipPath"
