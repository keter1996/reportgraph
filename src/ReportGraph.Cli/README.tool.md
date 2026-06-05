# Report Graph CLI

`reportgraph` is a .NET toolchain for initializing, diagnosing, refreshing, querying, watching, and deleting PBIP report graph artifacts.

Current refresh detection is based on stable PBIP source files rather than only coarse page/table counts. Markdown documents are included in the tracked source set, while generated `Graph/` output and Power BI local state such as `.pbi/` are excluded.

Common usage:

```powershell
reportgraph init
reportgraph status
reportgraph query page Page1
reportgraph update
reportgraph mark-dirty --reason "Changed outside ReportGraph"
reportgraph watch --refresh
reportgraph delete
```

`reportgraph doctor` now reports whether the current graph is stale, why it is stale, how many source files are being tracked, and the current source fingerprint.
`reportgraph query` automatically refreshes graph artifacts when the manifest is missing, the graph file is missing, or tracked source files have changed. MCP tool calls use the same refresh boundary.
`reportgraph mark-dirty` lets a host or operator defer refresh work until the next query or MCP call.
`reportgraph watch` lets local development workflows debounce tracked source-file changes and either mark the graph dirty or refresh it immediately.

Current MCP tools include:

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
