@echo off
setlocal
set GAME_DLL="C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Stardew Valley.dll"
set OUT_DIR="%~dp0..\decompiled\sdv-1.6.15-24356"

if not exist %GAME_DLL% (
    echo Error: Stardew Valley.dll not found at %GAME_DLL%
    exit /b 1
)

echo Decompiling Stardew Valley.dll into %OUT_DIR% ...
call "%~dp0ilspycmd.cmd" --nested-directories --project -o %OUT_DIR% %GAME_DLL%
if %ERRORLEVEL% EQU 0 (
    echo Done! Decompiled files are in %OUT_DIR%
) else (
    echo Error: Decompilation failed with exit code %ERRORLEVEL%
)
