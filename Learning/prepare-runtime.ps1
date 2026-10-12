param([string]$Destination = (Join-Path $PSScriptRoot '..\DownloadedBinaries\TutorRuntime'))
# Optional developer/test fixture. Application builds no longer bundle this tree;
# production first-use setup is implemented in TutorRuntimeInstallation.cs.
$ErrorActionPreference = 'Stop'
$runtimeVersion = '0.157.1'
$runtimeArchive = 'codex-app-server-package-x86_64-pc-windows-msvc.tar.gz'
$runtimeHash = '7ca926f4fc443c0ead83109314e9a97bf43d2da276be147f1919a4b5e62dfa8b'
$runtimeUrl = "https://github.com/openai/codex/releases/download/rust-v$runtimeVersion/$runtimeArchive"
$runtimeCache = Join-Path $PSScriptRoot '..\DownloadedBinaries\TutorCache'
New-Item -ItemType Directory -Force -Path $runtimeCache, $Destination | Out-Null
$archivePath = Join-Path $runtimeCache $runtimeArchive
if (!(Test-Path -LiteralPath $archivePath)) {
    Invoke-WebRequest -Uri $runtimeUrl -OutFile "$archivePath.partial"
    Move-Item -LiteralPath "$archivePath.partial" -Destination $archivePath
}
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $runtimeHash) { throw 'The official ChatGPT runtime checksum did not match.' }
$entries = & tar -tf $archivePath
if ($LASTEXITCODE -ne 0) { throw 'Could not read the runtime archive.' }
foreach ($entry in $entries) {
    if ($entry -match '(^[/\\]|(^|[/\\])\.\.([/\\]|$)|:)') { throw 'Unexpected runtime archive path.' }
}
& tar -xf $archivePath -C $Destination
if ($LASTEXITCODE -ne 0) { throw 'Could not extract the runtime archive.' }
$runtimeSignature = Get-AuthenticodeSignature -LiteralPath (Join-Path $Destination 'bin/codex-app-server.exe')
if ($runtimeSignature.Status -ne 'Valid' -or $runtimeSignature.SignerCertificate.GetNameInfo([System.Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false) -ne 'OpenAI OpCo, LLC') { throw 'The official ChatGPT runtime publisher signature did not verify.' }
foreach ($name in 'LICENSE','NOTICE') {
    Invoke-WebRequest -Uri "https://raw.githubusercontent.com/openai/codex/rust-v$runtimeVersion/$name" -OutFile (Join-Path $Destination $name)
}
@{version=$runtimeVersion;source=$runtimeUrl;sha256=$runtimeHash;license='Apache-2.0'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Destination 'STATSDIRECT-RUNTIME.json') -Encoding utf8
Write-Output "Prepared the official ChatGPT connection runtime $runtimeVersion. No credentials were copied."
