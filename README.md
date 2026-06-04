# ReportGraph / 报表图谱

语言 / Language: 中文 | [English](README.en.md)

ReportGraph 是一套面向 Power BI PBIP / PBIX 项目的可安装报表图谱工具链。它围绕报表图谱的构建、存储、查询与接入，提供统一 CLI、MCP stdio 与发布安装入口，并生成可查询、可复用的图谱产物。

## 快速开始

```powershell
git clone <git-url> ReportGraph
cd ReportGraph
powershell -ExecutionPolicy Bypass -File .\scripts\publish-reportgraph.ps1
.\artifacts\publish\win-x64\reportgraph.exe --help
```

在你的 PBIP 或 PBIX 项目中使用：

```powershell
cd <your-pbip-project-root>
reportgraph init
reportgraph status
reportgraph update
reportgraph query page <pageId>
reportgraph mcp
```

## 功能说明

- 从 PBIP 项目构建本地 Report Graph。
- 支持把 `.pbix` 作为入口，并通过托管转换边界接入标准 PBIP 主链路。
- 生成 `Graph/report-graph.json` 与 `Graph/manifest.json`。
- 生成 Markdown 上下文文件，方便人工阅读和 Agent 消费。
- 支持页面、页面绑定、表、视觉对象和轻量探索查询。
- 通过 MCP stdio 暴露图谱查询工具。
- 提供 Windows 自包含发布方式，适合客户机器和自动化安装场景。

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
reportgraph update [project-path-or-build-input-json] [--force]
reportgraph delete [project-path]
reportgraph status [project-path]
reportgraph query [project-path] <graph|page|page-bindings|table|visual|explore> ...
reportgraph mcp
reportgraph install-info
```

## MCP 工具

- `report.graph.load`
- `report.page.get`
- `report.page.bindings`
- `report.model.table.get`
- `report.visual.get`
- `report.graph.explore`

## 注意事项

- 本仓库不包含用户业务 PBIP 项目。
- 请使用你自己的 PBIP / PBIX 项目执行 `init`、`update` 和 `query`。
- 对于 `.pbix`，优先复用同目录已保存出的 PBIP；若没有可复用 PBIP，请先使用 Power BI Desktop 将其另存为 PBIP。
- 生成的 `Graph/` 产物通常不建议提交到 Git。
- Power BI 本地 `.pbi/` 状态文件不参与图谱稳定性计算。

## 文档

- [功能清单](docs/功能清单.md)
- [使用说明](docs/使用说明.md)
- [安装与接入说明](docs/安装与接入说明.md)
