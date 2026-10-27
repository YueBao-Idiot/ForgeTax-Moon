@echo off
rem Build the TianGong cheat menu into ..\Mods\TianGongCheatMod.dll
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
if errorlevel 1 pause
