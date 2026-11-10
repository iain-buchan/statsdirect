@echo off
setlocal
rem Signs one file with the StatsDirect code-signing certificate, then verifies the signature.
rem
rem By default, select the GlobalSign USB token certificate from the Windows certificate store.
rem AzureArtifactSigning uses the Windows SDK SignTool, the Artifact Signing plug-in and its profile metadata.
rem
rem   STATSDIRECT_SIGNING_SUBJECT        subject name to select, default "StatsDirect Ltd" (the certificate's common name)
rem   STATSDIRECT_SIGNING_THUMBPRINT     SHA-1 thumbprint to select instead of the name; use it if more than one certificate matches
rem   STATSDIRECT_SIGNING_TIMESTAMP_URL  RFC 3161 timestamp server, default for the selected provider
rem   STATSDIRECT_SIGNING_PASSWORD       only for the older way of signing with a Key\sdsign.pfx file, which is used if that file exists
rem   STATSDIRECT_SIGNING_SKIP_VERIFY    set to 1 to skip the trust check after signing (for a test certificate that Windows does not trust)
rem   STATSDIRECT_SIGNING_PROVIDER       set to AzureArtifactSigning to use Azure
rem   STATSDIRECT_SIGNING_SDK_TOOL       full path to the matching Windows SDK SignTool
rem   STATSDIRECT_SIGNING_AZURE_DLIB     full path to Azure.CodeSigning.Dlib.dll
rem   STATSDIRECT_SIGNING_AZURE_METADATA full path to the Artifact Signing profile metadata JSON
rem
rem Usage: sign.cmd <file>

cd /D "%~dp0"
if "%~1"=="" (
  echo sign.cmd: no file given
  exit /b 1
)
set "target=%~1"
if "%STATSDIRECT_SIGNING_PROVIDER%"=="AzureArtifactSigning" goto azure
if not "%STATSDIRECT_SIGNING_PROVIDER%"=="" (
  echo sign.cmd: unsupported signing provider
  exit /b 1
)
if "%STATSDIRECT_SIGNING_TIMESTAMP_URL%"=="" set "STATSDIRECT_SIGNING_TIMESTAMP_URL=http://timestamp.globalsign.com/tsa/r6advanced1"
if "%STATSDIRECT_SIGNING_SUBJECT%"=="" set "STATSDIRECT_SIGNING_SUBJECT=StatsDirect Ltd"

if exist sdsign.pfx goto pfx
if not "%STATSDIRECT_SIGNING_THUMBPRINT%"=="" goto thumbprint

echo sign.cmd: signing "%target%" with the certificate named "%STATSDIRECT_SIGNING_SUBJECT%"
signtool\signtool.exe sign /v /fd SHA256 /tr "%STATSDIRECT_SIGNING_TIMESTAMP_URL%" /td SHA256 /n "%STATSDIRECT_SIGNING_SUBJECT%" "%target%"
if errorlevel 1 exit /b 1
goto verify

:thumbprint
echo sign.cmd: signing "%target%" with the certificate %STATSDIRECT_SIGNING_THUMBPRINT%
signtool\signtool.exe sign /v /fd SHA256 /tr "%STATSDIRECT_SIGNING_TIMESTAMP_URL%" /td SHA256 /sha1 %STATSDIRECT_SIGNING_THUMBPRINT% "%target%"
if errorlevel 1 exit /b 1
goto verify

:pfx
echo sign.cmd: signing "%target%" with Key\sdsign.pfx
signtool\signtool.exe sign /v /fd SHA256 /tr "%STATSDIRECT_SIGNING_TIMESTAMP_URL%" /td SHA256 /f sdsign.pfx /p "%STATSDIRECT_SIGNING_PASSWORD%" "%target%"
if errorlevel 1 exit /b 1

:verify
if "%STATSDIRECT_SIGNING_SKIP_VERIFY%"=="1" (
  echo sign.cmd: signed; trust check skipped
  exit /b 0
)
signtool\signtool.exe verify /pa /v "%target%"
if errorlevel 1 (
  echo sign.cmd: "%target%" was signed but the signature does not verify
  exit /b 1
)
exit /b 0

:azure
if not exist "%STATSDIRECT_SIGNING_SDK_TOOL%" (
  echo sign.cmd: the Windows SDK SignTool was not found
  exit /b 1
)
if not exist "%STATSDIRECT_SIGNING_AZURE_DLIB%" (
  echo sign.cmd: the Azure Artifact Signing plug-in was not found
  exit /b 1
)
if not exist "%STATSDIRECT_SIGNING_AZURE_METADATA%" (
  echo sign.cmd: the Azure Artifact Signing metadata was not found
  exit /b 1
)
if "%STATSDIRECT_SIGNING_TIMESTAMP_URL%"=="" set "STATSDIRECT_SIGNING_TIMESTAMP_URL=http://timestamp.acs.microsoft.com"
echo sign.cmd: signing "%target%" with Azure Artifact Signing
"%STATSDIRECT_SIGNING_SDK_TOOL%" sign /v /fd SHA256 /tr "%STATSDIRECT_SIGNING_TIMESTAMP_URL%" /td SHA256 /dlib "%STATSDIRECT_SIGNING_AZURE_DLIB%" /dmdf "%STATSDIRECT_SIGNING_AZURE_METADATA%" "%target%"
if errorlevel 1 exit /b 1
"%STATSDIRECT_SIGNING_SDK_TOOL%" verify /pa /v /tw "%target%"
if errorlevel 1 exit /b 1
exit /b 0
