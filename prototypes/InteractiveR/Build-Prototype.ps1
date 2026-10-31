param(
    [string] $RHome = 'C:\Program Files\R\R-4.6.1',
    [string] $OutputDirectory = (Join-Path $PSScriptRoot 'bin')
)
$ErrorActionPreference = 'Stop'
$destination = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $destination -Force | Out-Null
& (Join-Path $PSScriptRoot 'Native\build-native.ps1') -RHome $RHome -OutputDirectory $destination
if ($LASTEXITCODE) { throw 'Native helper build failed.' }
& dotnet build (Join-Path $PSScriptRoot 'Windows\InteractiveR.csproj') -c Release -o $destination --nologo
if ($LASTEXITCODE) { throw 'Prototype host build failed.' }
Write-Output (Join-Path $destination 'StatsDirect.InteractiveR.Prototype.exe')
