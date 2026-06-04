@echo off
setlocal
set SCRIPT_DIR=%~dp0
for %%I in ("%SCRIPT_DIR%..") do set REPO_ROOT=%%~fI
dotnet run --project "%REPO_ROOT%\src\ReportGraph.Cli" -- %*
exit /b %ERRORLEVEL%
