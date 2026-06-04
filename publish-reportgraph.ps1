$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$artifactsDir = Join-Path $repoRoot 'artifacts'
$publishRoot = Join-Path $artifactsDir 'publish'
$runtime = 'win-x64'
$publishDir = Join-Path $publishRoot $runtime
$executableName = 'reportgraph.exe'

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

Push-Location $repoRoot
try {
    Get-ChildItem -Path $publishDir -Force | Remove-Item -Force -Recurse

    dotnet publish .\src\ReportGraph.Cli\ReportGraph.Cli.csproj `
        -c Release `
        -r $runtime `
        --self-contained true `
        /p:PublishSingleFile=true `
        /p:IncludeNativeLibrariesForSelfExtract=true `
        -o $publishDir

    Write-Host "Published reportgraph to $publishDir"
    Write-Host "Executable:"
    Write-Host "  $(Join-Path $publishDir $executableName)"
}
finally {
    Pop-Location
}
