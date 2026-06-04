@echo off
setlocal
set SCRIPT_DIR=%~dp0
dotnet run --project "%SCRIPT_DIR%src\ReportGraph.Cli" -- %*
exit /b %ERRORLEVEL%
