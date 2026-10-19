Add-Type -AssemblyName System.IO.Compression.FileSystem

$tempDir = Join-Path $env:TEMP ('jcs_' + (Get-Random))
$subDir = Join-Path $tempDir 'JunimoClientSync'
New-Item -ItemType Directory -Path $subDir -Force | Out-Null
Copy-Item -Path 'mods\JunimoClientSync\*' -Destination $subDir -Recurse -Force

$targets = @(
    'mods\JunimoServer\JunimoClientSync.zip',
    'tools\web-admin\public\JunimoClientSync.zip',
    'tools\web-admin\public\JunimoClientSync-v1.1.2.zip',
    'tools\web-admin\dist\JunimoClientSync.zip',
    'tools\web-admin\dist\JunimoClientSync-v1.1.2.zip'
)

foreach ($target in $targets) {
    $parent = Split-Path $target -Parent
    if (-not (Test-Path $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    if (Test-Path $target) {
        Remove-Item $target -Force
    }
    [System.IO.Compression.ZipFile]::CreateFromDirectory($tempDir, $target)
    Write-Host "Created: $target"
}

Remove-Item -Path $tempDir -Recurse -Force
