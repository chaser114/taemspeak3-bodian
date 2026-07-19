@echo off
setlocal
cd /d "%~dp0"

if not exist "%~dp0TS3AudioBot.exe" (
  echo TS3AudioBot.exe not found.
  exit /b 1
)

if exist "%~dp0packaging\common\prepare-data.ps1" (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0packaging\common\prepare-data.ps1" -Root "%~dp0" >nul
) else (
  if not exist "%~dp0data" mkdir "%~dp0data"
  if not exist "%~dp0data\bots" mkdir "%~dp0data\bots"
  if not exist "%~dp0data\ts3audiobot.toml" (
    >"%~dp0data\ts3audiobot.toml" echo [configs]
    >>"%~dp0data\ts3audiobot.toml" echo bots_path = "bots"
    >>"%~dp0data\ts3audiobot.toml" echo.
    >>"%~dp0data\ts3audiobot.toml" echo [db]
    >>"%~dp0data\ts3audiobot.toml" echo path = "ts3audiobot.db"
    >>"%~dp0data\ts3audiobot.toml" echo.
    >>"%~dp0data\ts3audiobot.toml" echo [rights]
    >>"%~dp0data\ts3audiobot.toml" echo path = "rights.toml"
    >>"%~dp0data\ts3audiobot.toml" echo.
    >>"%~dp0data\ts3audiobot.toml" echo [plugins]
    >>"%~dp0data\ts3audiobot.toml" echo path = "../plugins"
  )
)

rem Run with CWD=data so relative bots_path / db paths stay under data/.
cd /d "%~dp0data"
"%~dp0TS3AudioBot.exe" --config ts3audiobot.toml --non-interactive %*
