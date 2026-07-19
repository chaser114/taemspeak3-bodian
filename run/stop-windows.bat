@echo off
setlocal
cd /d "%~dp0\.."
echo Stopping TS3AudioBot for this install...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$root=(Resolve-Path '%cd%').Path; $pidFile=Join-Path $root 'ts3audiobot.pid'; if (Test-Path -LiteralPath $pidFile) { $p=Get-Content -LiteralPath $pidFile | Select-Object -First 1; if ($p) { Write-Host ('Stopping PID ' + $p + ' from pid file'); Stop-Process -Id $p -Force -ErrorAction SilentlyContinue }; Remove-Item -LiteralPath $pidFile -Force -ErrorAction SilentlyContinue }; Get-Process -Name 'TS3AudioBot' -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) } | ForEach-Object { Write-Host ('Stopping PID ' + $_.Id); Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }"
echo Done. Logs: logs\console.log and data\logs\
echo Start again: start-web-console.bat
exit /b 0
