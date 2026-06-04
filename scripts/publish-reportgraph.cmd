@echo off
setlocal
powershell -ExecutionPolicy Bypass -File "%~dp0publish-reportgraph.ps1"
exit /b %ERRORLEVEL%
