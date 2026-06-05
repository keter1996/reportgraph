# ReportGraph / 报表图谱

语言 / Language: 中文 | [English](README.en.md)

ReportGraph 是一套面向 Power BI PBIP 项目的可安装图谱工具链。它围绕报表图谱的构建、存储、查询与接入，提供统一 CLI、MCP stdio、发布脚本与安装入口，并生成可查询、可复用、对 Markdown 友好的本地产物。

## 快速开始

```powershell
git clone <git-url> ReportGraph
cd ReportGraph
powershell -ExecutionPolicy Bypass -File .\scripts\publish-reportgraph.ps1
.\artifacts\publish\win-x64\reportgraph.exe --help
```

进入你的 PBIP 项目目录后，常用流程如下：

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

## 功能说明

- 从 PBIP 项目构建本地 Report Graph。
- 生成 `Graph/report-graph.json`、`Graph/manifest.json`、`Graph/dirty-state.json` 和 Markdown 上下文文件。
- 跟踪 `.pbip`、`*.Report/**`、`*.SemanticModel/**` 与纳入扫描范围的 `.md` 文件。
- 排除 `Graph/`、`.pbi/`、`bin/`、`obj/`、`node_modules/` 和隐藏文件，避免误触发刷新。
- 支持页面、页面意图、页面上下文、绑定、度量值、度量血缘、术语、Markdown 文档、表、视觉对象和轻量 explore 查询。
- `doctor` 与 `status` 可显示 stale 状态、stale 原因、dirty 标记与源文件跟踪情况。
- `query` 和 MCP 工具会在消费前自动刷新缺失或过期的图谱。
- `mark-dirty` 支持宿主只做变更通知，延迟到下一次消费时再刷新。
- `watch` 支持本地开发时对源文件变化做 debounce 后标记 dirty 或直接刷新。

## 安装说明

推荐安装方式：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-reportgraph.ps1
```

发布完成后，可执行文件位于：

```text
artifacts\publish\win-x64\reportgraph.exe
```

运行：

```powershell
.\artifacts\publish\win-x64\reportgraph.exe --help
```

开发调试模式：

```powershell
dotnet build ReportGraph.slnx
dotnet run --project src/ReportGraph.Cli -- --help
```

可选 `.NET tool` 模式：

```powershell
.\scripts\install-reportgraph.ps1
.\artifacts\tool\reportgraph.exe --help
```

## 常用命令

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

## MCP 工具

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

## 注意事项

- 本仓库不包含业务 PBIP 项目。
- 请使用你自己的 PBIP 项目执行 `init`、`doctor`、`status`、`query` 和 `update`。
- 生成的 `Graph/` 产物通常不建议提交到 Git。
- Power BI 本地 `.pbi/` 状态文件不参与图谱稳定性判断。
- `doctor` 和 `status` 只做诊断，不会自动刷新。
- `query`、MCP 和 `watch --refresh` 才会触发自动刷新。

## 文档

- [功能清单](docs/功能清单.md)
- [使用说明](docs/使用说明.md)
- [安装与接入说明](docs/安装与接入说明.md)
