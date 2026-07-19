@echo off
cd /d "%~dp0"
echo Starting TS3AudioBot web console...
echo User data is stored in the data\ folder and is kept across upgrades.
call "%~dp0start.bat" %*
