cd /D "%~dp0"
set target="%1"
set password="%STATSDIRECT_SIGNING_PASSWORD%"
signtool\signtool.exe sign /v /f sdsign.pfx /fd SHA256 /t http://timestamp.digicert.com /p %password% %1
