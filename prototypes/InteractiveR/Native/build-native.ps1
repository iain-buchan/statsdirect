param(
    [string] $RHome = 'C:\Program Files\R\R-4.6.1',
    [string] $OutputDirectory = (Join-Path $PSScriptRoot 'bin'),
    [string] $Zig = (Join-Path $PSScriptRoot 'tools\zig-x86_64-windows-0.15.2\zig.exe')
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath (Join-Path $RHome 'include\R_ext\RStartup.h'))) { throw 'R development headers were not found.' }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$installation = if (Test-Path -LiteralPath $vswhere) { & $vswhere -latest -prerelease -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath }
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$source = Join-Path $PSScriptRoot 'console.c'
$manifest = Join-Path $PSScriptRoot 'console.manifest'
$exe = Join-Path $output 'StatsDirect.RConsole.Native.exe'
$object = Join-Path $output 'console.obj'
# This disposable sidecar deliberately uses the installed R through LoadLibrary,
# avoiding a toolchain-specific R import library. R API calls stay on its main thread.
if ($installation) {
    $developer = Join-Path $installation 'Common7\Tools\VsDevCmd.bat'
    $command = 'call "{0}" -no_logo -arch=x64 -host_arch=x64 && cl /nologo /TC /std:c11 /W4 /O2 /MD /utf-8 /DUNICODE /D_UNICODE /I"{1}\include" "{2}" /Fo"{3}" /Fe"{4}" /link /STACK:16777216 /MANIFEST:EMBED /MANIFESTINPUT:"{5}"' -f $developer, $RHome, $source, $object, $exe, $manifest
    & $env:ComSpec /d /s /c $command
} elseif (Test-Path -LiteralPath $Zig) {
    # Portable compiler fallback; does not install tools or change global PATH.
    $res = Join-Path $output 'console.res'
    Push-Location $PSScriptRoot
    try {
        & $Zig rc /fo $res 'console.rc'
        if ($LASTEXITCODE) { throw "Manifest resource compilation failed ($LASTEXITCODE)." }
        & $Zig cc -target x86_64-windows-gnu -std=c11 -O2 -Wall -Wextra -municode "-I$RHome\include" $source $res -o $exe '-Wl,--stack,16777216'
    } finally { Pop-Location }
} else {
    throw 'Install Visual Studio C++ tools, or extract verified portable Zig 0.15.2 under Native/tools.'
}
if ($LASTEXITCODE) { throw "Native helper build failed ($LASTEXITCODE)." }
Write-Output $exe
