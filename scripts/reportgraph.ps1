$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
dotnet run --project (Join-Path $repoRoot 'src\ReportGraph.Cli') -- @args
exit $LASTEXITCODE
