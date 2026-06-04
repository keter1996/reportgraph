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
            "query",
            "Query an existing Report Graph artifact set.",
            "reportgraph query [project-path] <graph|page|page-bindings|table|visual|explore> ...",
            ["q"]),
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
            "report.page.get",
            "Get a single page node with summary and story metadata.",
            "{ graphRoot: string, pageId: string }",
            "Returns the requested page node."),
        new(
            "report.page.bindings",
            "Get a page's table, measure, and visual field bindings.",
            "{ graphRoot: string, pageId: string }",
            "Returns binding details for the requested page."),
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
        "Codex can install the repo from a Git URL and use publish mode or tool mode to invoke the stable graph service boundary.",
        "CloudCode can consume the same repository layout and entrypoints because distribution stays outside core graph construction logic.",
        [
            "PBIP remains the primary input source. PBIX should first be converted into a temporary PBIP workspace.",
            "For customer environments without a .NET SDK or dotnet tool support, use publish-reportgraph.ps1 to produce a self-contained executable.",
            "Current CLI commands accept a project path, .pbip file, or build-input JSON contract. If no path is provided, current-directory commands operate against the current folder.",
            "Graph artifacts are stored under the project Graph directory and refreshed only when real source changes are detected.",
            "Markdown context files are first-class outputs so repositories with strong markdown workflows can consume graph summaries directly."
        ]);

    public IReadOnlyList<CliCommandDefinition> GetCliCommands() => CliCommands;

    public IReadOnlyList<McpToolDefinition> GetMcpTools() => McpTools;

    public InstallationGuide GetInstallationGuide() => Guide;
}
