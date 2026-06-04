# ReportGraph / 报表图谱

语言 / Language: 中文 | [English](README.en.md)

ReportGraph 是一个面向 Power BI PBIP 项目的本地图谱服务。它可以把报表结构、模型结构、页面关系和字段绑定生成可查询、可复用的图谱产物，并通过统一 CLI 与 MCP stdio 入口对外提供能力。

## 快速开始

```powershell
git clone <git-url> ReportGraph
cd ReportGraph
powershell -ExecutionPolicy Bypass -File .\publish-reportgraph.ps1
.\artifacts\publish\win-x64\reportgraph.exe --help
```

在你的 PBIP 项目中使用：

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
- 生成 `Graph/report-graph.json` 与 `Graph/manifest.json`。
- 生成 Markdown 上下文文件，方便人工阅读和 Agent 消费。
- 支持页面、页面绑定、表、视觉对象和轻量探索查询。
- 通过 MCP stdio 暴露图谱查询工具。
- 提供 Windows 自包含发布方式，适合客户机器和自动化安装场景。

## 安装说明

推荐安装方式：

```powershell
powershell -ExecutionPolicy Bypass -File .\publish-reportgraph.ps1
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
.\install-reportgraph.ps1
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
- 请使用你自己的 PBIP 项目执行 `init`、`update` 和 `query`。
- 生成的 `Graph/` 产物通常不建议提交到 Git。
- Power BI 本地 `.pbi/` 状态文件不参与图谱稳定性计算。

## 文档

- [功能清单](docs/功能清单.md)
- [使用说明](docs/使用说明.md)
- [安装与接入说明](docs/安装与接入说明.md)
