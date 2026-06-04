$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$artifactsDir = Join-Path $repoRoot 'artifacts'
$toolPath = Join-Path $artifactsDir 'tool'
$packRoot = Join-Path $artifactsDir 'nupkg'
$nupkgDir = Join-Path $packRoot ([Guid]::NewGuid().ToString('N'))

New-Item -ItemType Directory -Path $toolPath -Force | Out-Null
New-Item -ItemType Directory -Path $packRoot -Force | Out-Null
New-Item -ItemType Directory -Path $nupkgDir -Force | Out-Null

Push-Location $repoRoot
try {
    dotnet pack .\src\ReportGraph.Cli\ReportGraph.Cli.csproj -c Release -o $nupkgDir

    $packagePath = Get-ChildItem $nupkgDir -Filter 'ReportGraph.Tool.*.nupkg' |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1 -ExpandProperty FullName

    if (-not $packagePath) {
        throw 'No ReportGraph.Tool package was produced.'
    }

    cmd /c "dotnet tool uninstall ReportGraph.Tool --tool-path ""$toolPath"" >nul 2>nul"
    dotnet tool install ReportGraph.Tool --add-source $nupkgDir --tool-path $toolPath

    Write-Host "Installed reportgraph to $toolPath"
    Write-Host "Executable:"
    Write-Host "  $toolPath\reportgraph.exe"
}
finally {
    Pop-Location
}
