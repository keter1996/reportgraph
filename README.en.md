# ReportGraph

Language / 语言: [中文](README.md) | English

ReportGraph is an installable report-graph toolkit for Power BI PBIP projects. It provides a complete build, storage, query, and integration boundary for report graph artifacts through unified CLI, MCP stdio, and publish/install entrypoints.

## Quick Start

```powershell
git clone <git-url> ReportGraph
cd ReportGraph
powershell -ExecutionPolicy Bypass -File .\scripts\publish-reportgraph.ps1
.\artifacts\publish\win-x64\reportgraph.exe --help
```

Use it in your PBIP project:

```powershell
cd <your-pbip-project-root>
reportgraph init
reportgraph status
reportgraph update
reportgraph query page <pageId>
reportgraph mcp
```

## Features

- Build a local Report Graph from a PBIP project.
- Generate `Graph/report-graph.json` and `Graph/manifest.json`.
- Generate Markdown context files for human review and Agent consumption.
- Query pages, page bindings, tables, visuals, and lightweight graph exploration results.
- Expose graph query tools through MCP stdio.
- Provide a self-contained Windows publish path for customer machines and automated setup.

## Installation

Recommended installation mode:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-reportgraph.ps1
```

After publishing, the executable is located at:

```text
artifacts\publish\win-x64\reportgraph.exe
```

Run:

```powershell
.\artifacts\publish\win-x64\reportgraph.exe --help
```

Alternative developer mode:

```powershell
dotnet build ReportGraph.slnx
dotnet run --project src/ReportGraph.Cli -- --help
```

Optional .NET tool mode:

```powershell
.\scripts\install-reportgraph.ps1
.\artifacts\tool\reportgraph.exe --help
```

## Commands

```powershell
reportgraph init [project-path-or-build-input-json]
reportgraph update [project-path-or-build-input-json] [--force]
reportgraph delete [project-path]
reportgraph status [project-path]
reportgraph query [project-path] <graph|page|page-bindings|table|visual|explore> ...
reportgraph mcp
reportgraph install-info
```

## MCP Tools

- `report.graph.load`
- `report.page.get`
- `report.page.bindings`
- `report.model.table.get`
- `report.visual.get`
- `report.graph.explore`

## Notes

- This repository does not include business PBIP projects.
- Use your own PBIP project to run `init`, `update`, and `query`.
- Generated `Graph/` outputs should usually stay out of Git.
- Power BI local `.pbi/` state files are not part of graph stability calculation.

## Documentation

- [Feature List](docs/功能清单.md)
- [Usage Guide](docs/使用说明.md)
- [Installation And Integration Guide](docs/安装与接入说明.md)
