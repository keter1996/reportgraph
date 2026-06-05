# ReportGraph

Language / 语言: [中文](README.md) | English

ReportGraph is an installable report-graph toolkit for Power BI PBIP projects. It provides a complete build, storage, query, and integration boundary through a unified CLI, MCP stdio, publish scripts, and install entrypoints, while producing reusable local artifacts that work well with Markdown-centric workflows.

## Quick Start

```powershell
git clone <git-url> ReportGraph
cd ReportGraph
powershell -ExecutionPolicy Bypass -File .\scripts\publish-reportgraph.ps1
.\artifacts\publish\win-x64\reportgraph.exe --help
```

Inside your PBIP project, a typical flow looks like this:

```powershell
cd <your-pbip-project-root>
reportgraph init
reportgraph doctor
reportgraph status
reportgraph mark-dirty --reason "Changed outside ReportGraph"
reportgraph query page <pageId>
reportgraph watch
reportgraph mcp
```

## Features

- Build a local Report Graph from a PBIP project.
- Generate `Graph/report-graph.json`, `Graph/manifest.json`, `Graph/dirty-state.json`, and Markdown context output.
- Track `.pbip`, `*.Report/**`, `*.SemanticModel/**`, and included `.md` source files.
- Exclude `Graph/`, `.pbi/`, `bin/`, `obj/`, `node_modules/`, and hidden files to avoid false refresh triggers.
- Query page nodes, page intent, page context, bindings, measures, measure lineage, glossary terms, Markdown documents, tables, visuals, and lightweight exploration results.
- Show stale state, stale reason, dirty marks, and tracked-source status through `doctor` and `status`.
- Auto-refresh graph artifacts before `query` and MCP tool calls when the graph is missing or stale.
- Support host-driven lazy refresh through `mark-dirty`.
- Support local development debounce flows through `watch`, with either dirty-marking or immediate refresh behavior.

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
reportgraph doctor [project-path-or-pbip-file]
reportgraph update [project-path-or-build-input-json] [--force]
reportgraph delete [project-path]
reportgraph status [project-path]
reportgraph mark-dirty [project-path] [--reason <reason>]
reportgraph query [project-path] <graph|page|page-intent|page-context|page-bindings|measure|measure-lineage|term-search|document|table|visual|explore> ...
reportgraph watch [project-path] [--refresh] [--debounce-ms <milliseconds>]
reportgraph mcp
reportgraph install-info
```

## MCP Tools

- `report.graph.load`
- `report.graph.status`
- `report.graph.mark_dirty`
- `report.page.get`
- `report.page.intent`
- `report.page.context`
- `report.page.bindings`
- `report.measure.get`
- `report.measure.lineage`
- `report.term.search`
- `report.document.get`
- `report.model.table.get`
- `report.visual.get`
- `report.graph.explore`

## Notes

- This repository does not include business PBIP projects.
- Use your own PBIP project to run `init`, `doctor`, `status`, `query`, and `update`.
- Generated `Graph/` output should usually stay out of Git.
- Power BI local `.pbi/` state is excluded from graph stability checks.
- `doctor` and `status` are diagnostic-only and do not auto-refresh.
- Auto-refresh behavior is applied by `query`, MCP tool calls, and `watch --refresh`.

## Documentation

- [Feature List](docs/功能清单.md)
- [Usage Guide](docs/使用说明.md)
- [Installation And Integration Guide](docs/安装与接入说明.md)
