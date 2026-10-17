@echo off
setlocal enabledelayedexpansion
title Stardew Valley Mod EZ Setup & Auto-Sync
echo ========================================================
echo   STARDEW VALLEY SERVER MOD EZ-SETUP & AUTO-SYNC
echo ========================================================
echo.

set "SERVER_URL=https://stardew-api.ijplabs.dev"
set "MODS_DIR="

:: 1. Search for Steam default path
if exist "%ProgramFiles(x86)%\Steam\steamapps\common\Stardew Valley\Mods" (
    set "MODS_DIR=%ProgramFiles(x86)%\Steam\steamapps\common\Stardew Valley\Mods"
) else if exist "%ProgramFiles%\Steam\steamapps\common\Stardew Valley\Mods" (
    set "MODS_DIR=%ProgramFiles%\Steam\steamapps\common\Stardew Valley\Mods"
) else if exist "%LOCALAPPDATA%\Programs\Stardew Valley\Mods" (
    set "MODS_DIR=%LOCALAPPDATA%\Programs\Stardew Valley\Mods"
) else if exist "Mods" (
    set "MODS_DIR=%CD%\Mods"
)

if "%MODS_DIR%"=="" (
    echo [INFO] Folder Stardew Valley 'Mods' tidak ditemukan otomatis.
    echo Masukkan path folder Mods Anda (contoh: C:\Games\Stardew Valley\Mods):
    set /p "MODS_DIR=> "
)

if not exist "%MODS_DIR%" (
    echo Membuat folder Mods: %MODS_DIR%
    mkdir "%MODS_DIR%"
)

echo [INFO] Target folder Mods: %MODS_DIR%
echo [INFO] Mengunduh paket mod klien dari server: %SERVER_URL%...
echo.

powershell -NoProfile -Command "try { $wc = New-Object System.Net.WebClient; $wc.DownloadFile('%SERVER_URL%/api/mods/sync/client-mod', 'JunimoClientSync.zip'); Add-Type -AssemblyName System.IO.Compression.FileSystem; [System.IO.Compression.ZipFile]::ExtractToDirectory('JunimoClientSync.zip', '%MODS_DIR%', $true); Remove-Item 'JunimoClientSync.zip'; Write-Host '[OK] Mod JunimoClientSync berhasil dipasang ke folder Mods!' -ForegroundColor Green; } catch { Write-Host '[ERROR] Gagal mengunduh: ' $_.Exception.Message -ForegroundColor Red; }"

echo.
echo ========================================================
echo   Pemasangan selesai!
echo   Buka Stardew Valley (SMAPI), lalu klik tombol
echo   'Sync Server Mods' di layar Title / Lobby Co-op.
echo ========================================================
echo.
pause
