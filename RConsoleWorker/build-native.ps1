param(
    [string] $RHome = $env:R_HOME,
    [string] $OutputDirectory = (Join-Path $PSScriptRoot 'bin'),
    [string] $Zig = $env:STATSDIRECT_ZIG,
    [switch] $ProtectionChecks
)
$ErrorActionPreference = 'Stop'
# The application project passes these as empty strings when its properties are unset: the environment then applies, as documented.
if (!$RHome) { $RHome = $env:R_HOME }
if (!$Zig) { $Zig = $env:STATSDIRECT_ZIG }
function FileSha256([string] $Path) {
    $stream = [IO.File]::OpenRead($Path)
    $hash = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-', '') }
    finally { $hash.Dispose(); $stream.Dispose() }
}

# R headers are a build prerequisite, not a runtime installation bundled by StatsDirect.
if (!$RHome) {
    $installed = Get-ChildItem -LiteralPath (Join-Path $env:ProgramFiles 'R') -Directory -ErrorAction SilentlyContinue |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'include\R_ext\RStartup.h') } |
        Sort-Object { try { [version]($_.Name -replace '^R-', '') } catch { [version]'0.0' } } -Descending
    if ($installed) { $RHome = $installed[0].FullName }
}
if (!$RHome -or !(Test-Path -LiteralPath (Join-Path $RHome 'include\R_ext\RStartup.h'))) {
    throw 'R development headers were not found. Install 64-bit R, or pass -RHome / the RConsoleRHome build property.'
}
if (!$Zig) { $Zig = Join-Path $PSScriptRoot 'tools\zig-x86_64-windows-0.15.2\zig.exe' }
$RHome = [IO.Path]::GetFullPath($RHome)
# The R series the worker targets is pinned here, so that a newer R on the build machine cannot change what the release accepts.
$pin = Join-Path $PSScriptRoot 'target-r-series.txt'
if (!(Test-Path -LiteralPath $pin)) { throw 'RConsoleWorker/target-r-series.txt, the pinned R series such as 4.6, is missing from this checkout.' }
$targetSeries = (Get-Content -LiteralPath $pin -Raw).Trim()
$versionHeader = Get-Content -LiteralPath (Join-Path $RHome 'include\Rversion.h') -Raw
$headerMajor = [regex]::Match($versionHeader, '#define\s+R_MAJOR\s+"([^"]+)"').Groups[1].Value
$headerMinor = [regex]::Match($versionHeader, '#define\s+R_MINOR\s+"([^"]+)"').Groups[1].Value
$headerSeries = ($headerMajor + '.' + $headerMinor) -replace '\.[^.]*$', ''
if ($headerSeries -ne $targetSeries) {
    throw "The worker targets R $targetSeries (RConsoleWorker/target-r-series.txt); the headers at $RHome are R $headerSeries. Build with that R series, or change the target deliberately."
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$source = Join-Path $PSScriptRoot 'console.c'
$manifest = Join-Path $PSScriptRoot 'console.manifest'
$exe = Join-Path $output 'StatsDirect.RConsole.Native.exe'
$object = Join-Path $output 'console.obj'
$stamp = Join-Path $output 'build.json'
$sources = @($source, $manifest, (Join-Path $PSScriptRoot 'console.rc'), (Join-Path $PSScriptRoot 'msvc-rconfig.h'), $PSCommandPath,
    (Join-Path $RHome 'include\Rversion.h'), (Join-Path $RHome 'include\R_ext\RStartup.h'))
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$installation = if (Test-Path -LiteralPath $vswhere) {
    & $vswhere -latest -prerelease -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
}
# The compiler is part of the fingerprint: a change of toolchain rebuilds the worker and shows in its stamp.
# The pinned portable Zig is the compiler of record; Visual Studio's C compiler serves when Zig is absent, with two compatibility
# flags for R 4.6's headers (a C23 enum base type and C complex, see msvc-rconfig.h).
$useZig = Test-Path -LiteralPath $Zig
$compiler = if ($useZig) { 'zig:' + ((& $Zig version) -join '') + ':' + $Zig } elseif ($installation) { 'msvc:' + $installation } else { 'none' }
$fingerprint = (($sources | ForEach-Object { (FileSha256 $_) }) -join ':') + ':protectionChecks=' + [bool]$ProtectionChecks + ':compiler=' + $compiler
if ((Test-Path -LiteralPath $exe) -and (Test-Path -LiteralPath $stamp)) {
    $previous = Get-Content -LiteralPath $stamp -Raw | ConvertFrom-Json
    if ($previous.fingerprint -eq $fingerprint -and $previous.rHome -eq $RHome -and
        $previous.executableSha256 -eq (FileSha256 $exe)) {
        Write-Output $exe
        exit 0
    }
}

# R is loaded at runtime in this worker, never into the managed application.
# The import-library ABI does not depend on the chosen C compiler.
if ($useZig) {
    $res = Join-Path $output 'console.res'
    Push-Location $PSScriptRoot
    try {
        & $Zig rc /fo $res 'console.rc'
        if ($LASTEXITCODE) { throw "Manifest resource compilation failed ($LASTEXITCODE)." }
        $diagnosticArguments = @()
        if ($ProtectionChecks) { $diagnosticArguments = @('-DSTATSDIRECT_R_PROTECTION_CHECKS') }
        & $Zig cc -target x86_64-windows-gnu -std=c11 -O2 -Wall -Wextra -municode @diagnosticArguments "-I$RHome\include" $source $res -o $exe '-Wl,--stack,16777216'
    } finally { Pop-Location }
} elseif ($installation) {
    $developer = Join-Path $installation 'Common7\Tools\VsDevCmd.bat'
    $diagnosticDefine = if ($ProtectionChecks) { '/DSTATSDIRECT_R_PROTECTION_CHECKS' } else { '' }
    $command = 'call "{0}" -no_logo -arch=x64 -host_arch=x64 && cl /nologo /TC /std:c11 /W4 /O2 /MT /utf-8 /DUNICODE /D_UNICODE {6} /DR_LEGACY_RCOMPLEX /FI"{7}" /I"{1}\include" "{2}" /Fo"{3}" /Fe"{4}" /link /STACK:16777216 /MANIFEST:EMBED /MANIFESTINPUT:"{5}"' -f $developer, $RHome, $source, $object, $exe, $manifest, $diagnosticDefine, (Join-Path $PSScriptRoot 'msvc-rconfig.h')
    & $env:ComSpec /d /s /c $command
} else {
    throw 'Run RConsoleWorker/fetch-native-tools.ps1 for the verified portable Zig compiler, or set RConsoleZig / STATSDIRECT_ZIG to a Zig; Visual Studio C++ tools serve when neither is present.'
}
if ($LASTEXITCODE) { throw "Native helper build failed ($LASTEXITCODE) with $compiler. Run RConsoleWorker/fetch-native-tools.ps1 for the pinned Zig compiler, or set RConsoleZig / STATSDIRECT_ZIG." }
# Nothing installs the Visual C++ runtime for the worker: its import names must not reach for it.
$imports = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($exe))
if ($imports -match 'VCRUNTIME|MSVCP') { throw 'The worker imports the Visual C++ runtime, which StatsDirect does not install. Link the C runtime statically.' }
@{
    rHome = $RHome
    targetSeries = $targetSeries
    compiler = $compiler
    fingerprint = $fingerprint
    executableSha256 = (FileSha256 $exe)
    protectionChecks = [bool]$ProtectionChecks
} | ConvertTo-Json | Set-Content -LiteralPath $stamp -Encoding UTF8
Write-Output $exe
