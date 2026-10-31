# Optional portable compiler for machines without the Visual Studio C++ workload.
# The compiler stays inside this ignored scratch directory; nothing is installed.
$ErrorActionPreference = 'Stop'
$directory = Join-Path $PSScriptRoot 'tools'
$archive = Join-Path $directory 'zig-x86_64-windows-0.15.2.zip'
$expected = '3a0ed1e8799a2f8ce2a6e6290a9ff22e6906f8227865911fb7ddedc3cc14cb0c'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
if (!(Test-Path -LiteralPath $archive)) {
    Invoke-WebRequest -UseBasicParsing 'https://ziglang.org/download/0.15.2/zig-x86_64-windows-0.15.2.zip' -OutFile $archive
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expected) { throw 'Portable compiler SHA256 mismatch.' }
Expand-Archive -LiteralPath $archive -DestinationPath $directory -Force
Write-Output (Join-Path $directory 'zig-x86_64-windows-0.15.2\zig.exe')
