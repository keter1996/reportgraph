$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
dotnet run --project (Join-Path $scriptDir 'src\ReportGraph.Cli') -- @args
exit $LASTEXITCODE
