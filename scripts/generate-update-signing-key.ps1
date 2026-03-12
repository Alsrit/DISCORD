param(
    [string]$OutputDirectory = "deploy/keys",
    [string]$PrivateKeyName = "update-private.pem",
    [string]$PublicKeyName = "update-public.pem"
)

$ErrorActionPreference = "Stop"

$resolvedOutput = Resolve-Path . | ForEach-Object { Join-Path $_.Path $OutputDirectory }
New-Item -ItemType Directory -Force -Path $resolvedOutput | Out-Null

$privatePath = Join-Path $resolvedOutput $PrivateKeyName
$publicPath = Join-Path $resolvedOutput $PublicKeyName

$rsa = [System.Security.Cryptography.RSA]::Create(4096)

$privatePem = $rsa.ExportPkcs8PrivateKeyPem()
$publicPem = $rsa.ExportRSAPublicKeyPem()

[System.IO.File]::WriteAllText($privatePath, $privatePem)
[System.IO.File]::WriteAllText($publicPath, $publicPem)

Write-Host "Ключи подписи обновлений созданы:"
Write-Host "  Private: $privatePath"
Write-Host "  Public : $publicPath"
