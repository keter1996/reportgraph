using ReportGraph.Distribution.Models;

namespace ReportGraph.Distribution.Services;

public sealed class ReportGraphDistributionHost : IReportGraphDistributionHost
{
    private static readonly IReadOnlyList<CliCommandDefinition> CliCommands =
    [
        new(
            "init",
            "Initialize Graph artifacts for the current project or a provided project path.",
            "reportgraph init [project-path-or-build-input-json]",
            ["build", "refresh"]),
        new(
            "update",
            "Update Graph artifacts for the current project or a provided project path.",
            "reportgraph update [project-path-or-build-input-json] [--force]",
            ["u"]),
        new(
            "delete",
            "Delete Graph artifacts for the current project or a provided project path.",
            "reportgraph delete [project-path]",
            ["remove", "rm"]),
        new(
            "status",
            "Show Graph status for the current project or a provided project path.",
            "reportgraph status [project-path]",
            ["st"]),
        new(
            "doctor",
            "Validate PBIP project structure and ReportGraph entry prerequisites.",
            "reportgraph doctor [project-path-or-pbip-file]",
            ["diag", "validate"]),
        new(
            "mark-dirty",
            "Mark the current project or a provided project path as dirty without rebuilding immediately.",
            "reportgraph mark-dirty [project-path] [--reason <reason>]",
            ["dirty", "notify"]),
        new(
            "query",
            "Query an existing Report Graph artifact set.",
            "reportgraph query [project-path] <graph|page|page-intent|page-context|page-bindings|measure|measure-lineage|term-search|document|table|visual|explore> ...",
            ["q"]),
        new(
            "watch",
            "Watch a PBIP project and mark it dirty or refresh it when tracked source files change.",
            "reportgraph watch [project-path] [--refresh] [--debounce-ms <milliseconds>]",
            []),
        new(
            "mcp",
            "Run the ReportGraph MCP server over stdio.",
            "reportgraph mcp",
            []),
        new(
            "install-info",
            "Show installation and host integration guidance.",
            "reportgraph install-info",
            ["info"])
    ];

    private static readonly IReadOnlyList<McpToolDefinition> McpTools =
    [
        new(
            "report.graph.load",
            "Load the current report graph for a PBIP project or artifact root.",
            "{ projectRoot?: string, graphRoot?: string }",
            "Returns the full Report Graph payload."),
        new(
            "report.graph.status",
            "Inspect graph availability, stale state, dirty state, and tracked source summary without refreshing.",
            "{ projectRoot?: string, graphRoot?: string }",
            "Returns graph status, stale reason, dirty state, and a lightweight graph summary."),
        new(
            "report.graph.mark_dirty",
            "Mark a PBIP project graph as dirty so the next query or MCP call can refresh lazily.",
            "{ projectRoot?: string, graphRoot?: string, reason?: string }",
            "Returns the updated dirty-state summary for the project."),
        new(
            "report.page.get",
            "Get a single page node with summary and story metadata.",
            "{ graphRoot: string, pageId: string }",
            "Returns the requested page node."),
        new(
            "report.page.intent",
            "Get a page's semantic topic, primary question, reading order, and primary visuals.",
            "{ graphRoot: string, pageId: string }",
            "Returns semantic page-intent details for the requested page."),
        new(
            "report.page.context",
            "Get a page's default filters, visual filters, and common slicer entry points.",
            "{ graphRoot: string, pageId: string }",
            "Returns semantic context details for the requested page."),
        new(
            "report.page.bindings",
            "Get a page's table, measure, and visual field bindings.",
            "{ graphRoot: string, pageId: string }",
            "Returns binding details for the requested page."),
        new(
            "report.measure.get",
            "Get a measure's business name, dependency lineage, and semantic classification.",
            "{ graphRoot: string, measureName: string, tableName?: string }",
            "Returns semantic details for the requested measure."),
        new(
            "report.measure.lineage",
            "Get a recursive measure dependency graph with terminal column dependencies.",
            "{ graphRoot: string, measureName: string, tableName?: string }",
            "Returns measure nodes, measure-to-measure edges, and measure-to-column edges."),
        new(
            "report.term.search",
            "Search business terms by display name, alias, or mapped graph object.",
            "{ graphRoot: string, query: string }",
            "Returns matching glossary terms and mapped report objects."),
        new(
            "report.document.get",
            "Get a Markdown document index node with summary, keywords, and linked graph objects.",
            "{ graphRoot: string, documentIdOrPath: string }",
            "Returns the requested indexed Markdown document node."),
        new(
            "report.model.table.get",
            "Get where a model table is used across pages and visuals.",
            "{ graphRoot: string, tableName: string }",
            "Returns usage details for the requested table."),
        new(
            "report.visual.get",
            "Get a visual and all of its bound fields.",
            "{ graphRoot: string, visualId: string }",
            "Returns the requested visual node and bindings."),
        new(
            "report.graph.explore",
            "Run lightweight traversal queries from page or table entry points.",
            "{ graphRoot: string, mode: string, key: string }",
            "Returns related pages, tables, measures, and visuals.")
    ];

    private static readonly InstallationGuide Guide = new(
        "git clone <git-url> ReportGraph",
        "dotnet build ReportGraph.slnx",
        "dotnet run --project src/ReportGraph.Cli -- <command>",
        "Codex can install the repo from a Git URL and use publish mode or tool mode to invoke the stable report-graph service boundary.",
        "CloudCode can consume the same repository layout and entrypoints because distribution stays outside core graph construction logic.",
        [
            "PBIP is the supported primary input source for ReportGraph.",
            "For customer environments without a .NET SDK or dotnet tool support, use scripts\\publish-reportgraph.ps1 to produce a self-contained executable.",
            "Current CLI commands accept a project path, .pbip file, or build-input JSON contract. If no path is provided, current-directory commands operate against the current folder.",
            "Graph artifacts are stored under the project Graph directory and refreshed only when real source changes are detected.",
            "CLI query commands automatically refresh graph artifacts when the manifest is missing, the graph is missing, or tracked source files have changed.",
            "MCP tool calls use the same stale-detection boundary and can auto-refresh graph artifacts before returning structured results.",
            "Use reportgraph mark-dirty when a host has already detected external changes and wants ReportGraph to refresh lazily on the next query or MCP call.",
            "Use reportgraph watch during local development when you want ReportGraph to observe tracked source-file changes and mark the graph dirty or auto-refresh with debounce.",
            "Markdown context files are first-class outputs so repositories with strong markdown workflows can consume graph summaries directly."
        ]);

    public IReadOnlyList<CliCommandDefinition> GetCliCommands() => CliCommands;

    public IReadOnlyList<McpToolDefinition> GetMcpTools() => McpTools;

    public InstallationGuide GetInstallationGuide() => Guide;
}
