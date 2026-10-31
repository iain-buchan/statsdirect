param(
    [string] $RHome = $env:R_HOME,
    [string] $OutputDirectory = (Join-Path $PSScriptRoot 'bin'),
    [string] $Zig = $env:STATSDIRECT_ZIG
)
$ErrorActionPreference = 'Stop'
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
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$source = Join-Path $PSScriptRoot 'console.c'
$manifest = Join-Path $PSScriptRoot 'console.manifest'
$exe = Join-Path $output 'StatsDirect.RConsole.Native.exe'
$object = Join-Path $output 'console.obj'
$stamp = Join-Path $output 'build.json'
$sources = @($source, $manifest, (Join-Path $PSScriptRoot 'console.rc'), $PSCommandPath,
    (Join-Path $RHome 'include\Rversion.h'), (Join-Path $RHome 'include\R_ext\RStartup.h'))
$fingerprint = ($sources | ForEach-Object { (FileSha256 $_) }) -join ':'
if ((Test-Path -LiteralPath $exe) -and (Test-Path -LiteralPath $stamp)) {
    $previous = Get-Content -LiteralPath $stamp -Raw | ConvertFrom-Json
    if ($previous.fingerprint -eq $fingerprint -and $previous.rHome -eq $RHome -and
        $previous.executableSha256 -eq (FileSha256 $exe)) {
        Write-Output $exe
        exit 0
    }
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$installation = if (Test-Path -LiteralPath $vswhere) {
    & $vswhere -latest -prerelease -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
}
# R is loaded at runtime in this worker, never into the managed application.
# The import-library ABI does not depend on the chosen C compiler.
if ($installation) {
    $developer = Join-Path $installation 'Common7\Tools\VsDevCmd.bat'
    $command = 'call "{0}" -no_logo -arch=x64 -host_arch=x64 && cl /nologo /TC /std:c11 /W4 /O2 /MD /utf-8 /DUNICODE /D_UNICODE /I"{1}\include" "{2}" /Fo"{3}" /Fe"{4}" /link /STACK:16777216 /MANIFEST:EMBED /MANIFESTINPUT:"{5}"' -f $developer, $RHome, $source, $object, $exe, $manifest
    & $env:ComSpec /d /s /c $command
} elseif (Test-Path -LiteralPath $Zig) {
    $res = Join-Path $output 'console.res'
    Push-Location $PSScriptRoot
    try {
        & $Zig rc /fo $res 'console.rc'
        if ($LASTEXITCODE) { throw "Manifest resource compilation failed ($LASTEXITCODE)." }
        & $Zig cc -target x86_64-windows-gnu -std=c11 -O2 -Wall -Wextra -municode "-I$RHome\include" $source $res -o $exe '-Wl,--stack,16777216'
    } finally { Pop-Location }
} else {
    throw 'Install Visual Studio C++ tools, or run RConsoleWorker/fetch-native-tools.ps1 for the verified portable Zig compiler. RConsoleZig / STATSDIRECT_ZIG can select an existing compiler.'
}
if ($LASTEXITCODE) { throw "Native helper build failed ($LASTEXITCODE)." }
@{
    rHome = $RHome
    fingerprint = $fingerprint
    executableSha256 = (FileSha256 $exe)
} | ConvertTo-Json | Set-Content -LiteralPath $stamp -Encoding UTF8
Write-Output $exe
