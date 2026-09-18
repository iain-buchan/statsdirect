@echo off
setlocal
rem Signs one file with the StatsDirect code-signing certificate, then verifies the signature.
rem
rem The certificate lives on a GlobalSign USB token (SafeNet client), so it is selected from the Windows certificate store
rem by name or thumbprint and the token client asks for the token password. Nothing secret is stored in this repository.
rem
rem   STATSDIRECT_SIGNING_SUBJECT        subject name to select, default "StatsDirect Ltd" (the certificate's common name)
rem   STATSDIRECT_SIGNING_THUMBPRINT     SHA-1 thumbprint to select instead of the name; use it if more than one certificate matches
rem   STATSDIRECT_SIGNING_TIMESTAMP_URL  RFC 3161 timestamp server, default GlobalSign's
rem   STATSDIRECT_SIGNING_PASSWORD       only for the older way of signing with a Key\sdsign.pfx file, which is used if that file exists
rem   STATSDIRECT_SIGNING_SKIP_VERIFY    set to 1 to skip the trust check after signing (for a test certificate that Windows does not trust)
rem
rem Usage: sign.cmd <file>

cd /D "%~dp0"
if "%~1"=="" (
  echo sign.cmd: no file given
  exit /b 1
)
set "target=%~1"
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
