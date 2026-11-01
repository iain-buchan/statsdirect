param(
    [Parameter(Mandatory)][string]$ApiIncludeDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$Zig = $env:STATSDIRECT_ZIG
)
$ErrorActionPreference = 'Stop'
$ApiIncludeDirectory = [IO.Path]::GetFullPath($ApiIncludeDirectory)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
foreach ($header in @('BootstrapperApplicationTypes.h', 'IBootstrapperEngine.h')) {
    if (!(Test-Path -LiteralPath (Join-Path $ApiIncludeDirectory $header))) { throw "WiX native API header missing: $header. Restore WixToolset.BootstrapperApplicationApi 6.0.2." }
}
if (!$Zig) { $Zig = Join-Path $PSScriptRoot '..\..\RConsoleWorker\tools\zig-x86_64-windows-0.15.2\zig.exe' }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$installation = if (Test-Path -LiteralPath $vswhere) { & $vswhere -latest -prerelease -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath }
$useZig = Test-Path -LiteralPath $Zig
$compiler = if ($useZig) { 'zig:' + ((& $Zig version) -join '') + ':' + $Zig } elseif ($installation) { 'msvc:' + $installation } else { throw 'The setup focus helper requires the pinned Zig compiler or Visual Studio C++ build tools.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$dll = Join-Path $OutputDirectory 'StatsDirect.Setup.Focus.dll'
$stamp = Join-Path $OutputDirectory 'build.json'
function Hash([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $hash = [Security.Cryptography.SHA256]::Create()
    try { [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-', '') }
    finally { $hash.Dispose(); $stream.Dispose() }
}
$inputs = @($PSCommandPath, (Join-Path $PSScriptRoot 'Focus.cpp'), (Join-Path $PSScriptRoot 'FocusApi.h')) + @(Get-ChildItem -LiteralPath $ApiIncludeDirectory -Filter '*.h' | Sort-Object Name | ForEach-Object FullName)
$fingerprint = (($inputs | ForEach-Object { Hash $_ }) -join ':') + ':' + $compiler
if ((Test-Path -LiteralPath $dll) -and (Test-Path -LiteralPath $stamp)) {
    $previous = Get-Content -LiteralPath $stamp -Raw | ConvertFrom-Json
    if ($previous.fingerprint -eq $fingerprint -and $previous.sha256 -eq (Hash $dll)) { Write-Output $dll; exit 0 }
}
$source = Join-Path $PSScriptRoot 'Focus.cpp'
if ($useZig) {
    & $Zig c++ -target x86_64-windows-gnu -std=c++17 -fms-extensions -fno-exceptions -fno-rtti -nostdlib++ -O2 -Wall -Wextra -shared "-I$ApiIncludeDirectory" $source -o $dll -luser32
} else {
    $environment = Join-Path $installation 'VC\Auxiliary\Build\vcvars64.bat'
    $object = Join-Path $OutputDirectory 'Focus.obj'
    $command = 'call "{0}" >nul && cl.exe /nologo /O2 /W4 /MT /EHsc /std:c++17 /LD /I"{1}" /Fo"{2}" "{3}" /link /OUT:"{4}" /IMPLIB:"{5}\Focus.lib" /DYNAMICBASE /NXCOMPAT user32.lib' -f $environment, $ApiIncludeDirectory, $object, $source, $dll, $OutputDirectory
    & $env:ComSpec /d /s /c $command
}
if ($LASTEXITCODE) { throw "Setup focus helper build failed ($LASTEXITCODE)." }
$binary = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($dll))
if ($binary -match 'VCRUNTIME|MSVCP') { throw 'The setup focus helper must not require a separate Visual C++ runtime.' }
@{ fingerprint = $fingerprint; compiler = $compiler; sha256 = (Hash $dll) } | ConvertTo-Json | Set-Content -LiteralPath $stamp -Encoding utf8
Write-Output $dll
