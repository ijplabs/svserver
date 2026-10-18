# Script to generate Code Signing Certificate and sign JunimoClientSync.dll
$certName = "CN=Junimo Dedicated Server Security Authority, O=JunimoServer Mod Security, C=ID"
$existing = Get-ChildItem -Path Cert:\CurrentUser\My | Where-Object { $_.Subject -like "*Junimo Dedicated Server Security Authority*" } | Select-Object -First 1

if (-not $existing) {
    Write-Host "Generating new Code Signing Certificate..."
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $certName -CertStoreLocation "Cert:\CurrentUser\My" -NotAfter (Get-Date).AddYears(10)
} else {
    Write-Host "Using existing Code Signing Certificate: $($existing.Thumbprint)"
    $cert = $existing
}

# Export public certificate
$repoRoot = Resolve-Path "."
$certPaths = @(
    "$repoRoot\mods\JunimoClientSync\JunimoSecurityCert.cer",
    "$repoRoot\mod\JunimoClientSync\JunimoSecurityCert.cer",
    "$repoRoot\tools\web-admin\public\JunimoSecurityCert.cer"
)

foreach ($cp in $certPaths) {
    $parent = Split-Path $cp -Parent
    if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    Export-Certificate -Cert $cert -FilePath $cp -Force | Out-Null
    Write-Host "Exported public certificate to $cp"
}

# Sign DLL
$dllPath = "$repoRoot\mods\JunimoClientSync\JunimoClientSync.dll"
if (Test-Path $dllPath) {
    $sig = Set-AuthenticodeSignature -FilePath $dllPath -Certificate $cert -HashAlgorithm SHA256
    Write-Host "Signature Status: $($sig.Status) - $($sig.StatusMessage)"
} else {
    Write-Host "DLL not found at $dllPath" -ForegroundColor Red
}

# Also sign mod/JunimoClientSync/bin DLL if present
$binDll = "$repoRoot\mod\JunimoClientSync\bin\Release\net6.0\JunimoClientSync.dll"
if (Test-Path $binDll) {
    Set-AuthenticodeSignature -FilePath $binDll -Certificate $cert -HashAlgorithm SHA256 | Out-Null
}
