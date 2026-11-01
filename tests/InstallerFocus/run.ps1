param(
    [Parameter(Mandatory)][string]$ApiIncludeDirectory,
    [Parameter(Mandatory)][string]$NativeDll,
    [Parameter(Mandatory)][string]$Zig,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'bin')
)
$ErrorActionPreference = 'Stop'
$ApiIncludeDirectory = [IO.Path]::GetFullPath($ApiIncludeDirectory)
$NativeDll = [IO.Path]::GetFullPath($NativeDll)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
foreach ($path in @($NativeDll, $Zig, (Join-Path $ApiIncludeDirectory 'BootstrapperApplicationTypes.h'))) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Test prerequisite missing: $path" }
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$exe = Join-Path $OutputDirectory 'InstallerFocusTests.exe'
& $Zig c++ -target x86_64-windows-gnu -std=c++17 -fms-extensions -O2 -Wall -Wextra -municode "-I$ApiIncludeDirectory" (Join-Path $PSScriptRoot 'FocusTests.cpp') -o $exe -luser32
if ($LASTEXITCODE) { throw "Installer-focus checks could not compile ($LASTEXITCODE)." }
& $exe $NativeDll
if ($LASTEXITCODE) { throw "Installer-focus checks failed ($LASTEXITCODE)." }
