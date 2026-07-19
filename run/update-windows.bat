@echo off
setlocal
cd /d "%~dp0\.."
if "%~1"=="" (
  echo 用法: update-windows.bat ^<新版本目录或.zip^>
  echo 示例: update-windows.bat ..\TS3AudioBot-KuwoPlugin-windows-x64-new
  echo 说明: 只会替换程序文件，data\ 里的机器人与账号不会被覆盖。
  exit /b 0
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0update-windows.ps1" -Source "%~1" -InstallDir "%cd%"
exit /b %errorlevel%
