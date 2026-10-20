param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ArgsList
)

$DotnetExe = "C:\Program Files\dotnet\dotnet.exe"
if (!(Test-Path $DotnetExe)) {
    $DotnetExe = (Get-Command dotnet -ErrorAction SilentlyContinue)?.Source
}

$IlSpyCmdDll = Join-Path $PSScriptRoot "ilspycmd\ilspycmd.dll"
$DefaultGameDll = "C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Stardew Valley.dll"

# If no assembly file argument is provided, append default game DLL
$hasAssembly = $false
foreach ($arg in $ArgsList) {
    if ($arg.EndsWith(".dll", [System.StringComparison]::OrdinalIgnoreCase) -or $arg.EndsWith(".exe", [System.StringComparison]::OrdinalIgnoreCase)) {
        $hasAssembly = $true
        break
    }
}

$finalArgs = @("exec", $IlSpyCmdDll)
if ($ArgsList) {
    $finalArgs += $ArgsList
}

if (!$hasAssembly -and (Test-Path $DefaultGameDll)) {
    $finalArgs += $DefaultGameDll
}

& $DotnetExe @finalArgs
