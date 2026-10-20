param(
    [string]$GameDll = "C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Stardew Valley.dll",
    [string]$OutputDir = (Join-Path $PSScriptRoot "..\decompiled\sdv-1.6.15-24356")
)

if (!(Test-Path $GameDll)) {
    Write-Error "Stardew Valley.dll not found at: $GameDll"
    exit 1
}

Write-Host "Decompiling Stardew Valley.dll into $OutputDir ..."
& (Join-Path $PSScriptRoot "ilspycmd.cmd") --nested-directories --project -o $OutputDir $GameDll

if ($LASTEXITCODE -eq 0) {
    Write-Host "Done! Decompiled source code is available in $OutputDir"
} else {
    Write-Error "Decompilation failed with exit code $LASTEXITCODE"
}
