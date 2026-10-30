@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Watch-NoraAgentLog.ps1" -Follow
pause
