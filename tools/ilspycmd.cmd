@echo off
set DOTNET_EXE="C:\Program Files\dotnet\dotnet.exe"
set ILSPY_DLL="%~dp0ilspycmd\ilspycmd.dll"
set DEFAULT_GAME_DLL="C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Stardew Valley.dll"

rem Check if any argument contains .dll
set HAS_DLL=0
for %%x in (%*) do (
    echo "%%~xx" | findstr /i "\.dll \.exe" >nul && set HAS_DLL=1
)

if "%HAS_DLL%"=="1" (
    %DOTNET_EXE% exec %ILSPY_DLL% %*
) else (
    %DOTNET_EXE% exec %ILSPY_DLL% %* %DEFAULT_GAME_DLL%
)
