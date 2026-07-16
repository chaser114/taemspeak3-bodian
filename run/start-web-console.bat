@echo off
setlocal
cd /d "%~dp0\.."
if exist "%~dp0..\TS3AudioBot.exe" (
  call "%~dp0..\start-web-console.bat" %*
  exit /b %errorlevel%
)
echo This launcher must be used from a published Windows package.
pause
exit /b 1
