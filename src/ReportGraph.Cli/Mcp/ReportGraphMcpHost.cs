using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ReportGraph.Adapters.Services;
using ReportGraph.Core.Services;
using ReportGraph.HostIntegration.Services;
using ReportGraph.Query.Services;
using ReportGraph.Storage.Storage;

namespace ReportGraph.Cli.Mcp;

internal static class ReportGraphMcpHost
{
    public static async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<IReportGraphBuildInputAdapter, ReportGraphProjectAdapter>();
        builder.Services.AddSingleton<IReportGraphBuilder, ReportGraphBuilder>();
        builder.Services.AddSingleton<IReportGraphContextRenderer, ReportGraphContextRenderer>();
        builder.Services.AddSingleton<IReportGraphFingerprintService, ReportGraphFingerprintService>();
        builder.Services.AddSingleton<IReportGraphStalenessChecker, ReportGraphStalenessChecker>();
        builder.Services.AddSingleton<IReportGraphQueryService, ReportGraphQueryService>();
        builder.Services.AddSingleton<IReportGraphFileStore, ReportGraphFileStore>();
        builder.Services.AddSingleton<IReportGraphContextFileStore, ReportGraphContextFileStore>();
        builder.Services.AddSingleton<IReportGraphService, ReportGraphService>();
        builder.Services.AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(ReportGraphMcpTools).Assembly);

        using var host = builder.Build();
        await host.RunAsync(cancellationToken);
    }
}

[McpServerToolType]
internal sealed class ReportGraphMcpTools
{
    private readonly IReportGraphBuildInputAdapter buildInputAdapter;
    private readonly IReportGraphService graphService;
    private readonly IReportGraphQueryService queryService;

    public ReportGraphMcpTools(
        IReportGraphBuildInputAdapter buildInputAdapter,
        IReportGraphService graphService,
        IReportGraphQueryService queryService)
    {
        this.buildInputAdapter = buildInputAdapter;
        this.graphService = graphService;
        this.queryService = queryService;
    }

    [McpServerTool(Name = "report.graph.load")]
    public async Task<object> LoadGraphAsync(
        string? projectRoot = null,
        string? graphRoot = null,
        CancellationToken cancellationToken = default)
    {
        var graph = await LoadRequiredGraphAsync(projectRoot, graphRoot, cancellationToken);
        return queryService.GetGraph(graph);
    }

    [McpServerTool(Name = "report.graph.status")]
    public async Task<object> GetGraphStatusAsync(
        string? projectRoot = null,
        string? graphRoot = null,
        CancellationToken cancellationToken = default)
    {
        var input = await LoadBuildInputAsync(projectRoot, graphRoot, cancellationToken);
        var refreshState = await graphService.EvaluateRefreshStateAsync(input, cancellationToken);
        var graph = await graphService.LoadAsync(input.Source.PbipProjectPath, cancellationToken);

        return new
        {
            projectRoot = input.Source.PbipProjectPath,
            reportRoot = input.Source.ReportRootPath,
            modelName = input.Source.ModelName,
            graphDirectoryExists = refreshState.GraphDirectoryExists,
            graphFileExists = refreshState.GraphFileExists,
            manifestExists = refreshState.ManifestExists,
            dirtyMarkExists = refreshState.DirtyState is not null,
            graphStale = refreshState.IsStale,
            staleReason = refreshState.Reason,
            sourceFilesTracked = refreshState.SourceFileCount,
            sourceFingerprint = refreshState.SourceFingerprint,
            dirtyState = refreshState.DirtyState,
            graphSummary = graph is null
                ? null
                : new
                {
                    reportName = graph.Report.ReportName,
                    pageCount = graph.Report.Pages.Count,
                    tableCount = graph.Model.Tables.Count
                }
        };
    }

    [McpServerTool(Name = "report.graph.mark_dirty")]
    public async Task<object> MarkGraphDirtyAsync(
        string? reason = null,
        string? projectRoot = null,
        string? graphRoot = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedProjectRoot = ResolveProjectRoot(projectRoot, graphRoot);
        var effectiveReason = string.IsNullOrWhiteSpace(reason)
            ? "Marked dirty by MCP host notification"
            : reason;

        await graphService.MarkDirtyAsync(resolvedProjectRoot, effectiveReason, cancellationToken);
        var input = await buildInputAdapter.LoadAsync(resolvedProjectRoot, cancellationToken);
        var refreshState = await graphService.EvaluateRefreshStateAsync(input, cancellationToken);

        return new
        {
            projectRoot = resolvedProjectRoot,
            dirtyMarkExists = refreshState.DirtyState is not null,
            staleReason = refreshState.Reason,
            dirtyState = refreshState.DirtyState
        };
    }

    [McpServerTool(Name = "report.page.get")]
    public async Task<object?> GetPageAsync(
        string pageId,
        string? projectRoot = null,
        string? graphRoot = null,
        CancellationToken cancellationToken = default)
    {
        var graph = await LoadRequiredGraphAsync(projectRoot, graphRoot, cancellationToken);
        return queryService.GetPage(graph, pageId);
    }

    [McpServerTool(Name = "report.page.intent")]
    public async Task<object?> GetPageIntentAsync(
        string pageId,
        string? projectRoot = null,
        string? graphRoot = null,
        CancellationToken cancellationToken = default)
    {
        var graph = await LoadRequiredGraphAsync(projectRoot, graphRoot, cancellationToken);
        return queryService.GetPageIntent(graph, pageId);
    }

    [McpServerTool(Name = "report.page.context")]
    public async Task<object?> GetPageContextAsync(
        string pageId,
        string? projectRoot = null,
        string? graphRoot = null,
        CancellationToken cancellationToken = default)
    {
        var graph = await LoadRequiredGraphAsync(projectRoot, graphRoot, cancellationToken);
        return queryService.GetPageContext(graph, pageId);
    }

    [McpServerTool(Name = "report.page.bindings")]
    public async Task<object?> GetPageBindingsAsync(
        string pageId,
        string? projectRoot = null,
        string? graphRoot = null,
        CancellationToken cancellationToken = default)
    {
        var graph = await LoadRequiredGraphAsync(projectRoot, graphRoot, cancellationToken);
        return queryService.GetPageBindings(graph, pageId);
    }

    [McpServerTool(Name = "report.measure.get")]
    public async Task<object?> GetMeasureAsync(
        string measureName,
        string? tableName = null,
        string? projectRoot = null,
        string? graphRoot = null,
        CancellationToken cancellationToken = default)
    {
        var graph = await LoadRequiredGraphAsync(projectRoot, graphRoot, cancellationToken);
        return queryService.GetMeasure(graph, measureName, tableName);
    }

    [McpServerTool(Name = "report.measure.lineage")]
    public async Task<object?> GetMeasureLineageAsync(
        string measureName,
        string? tableName = null,
        string? projectRoot = null,
        string? graphRoot = null,
        CancellationToken cancellationToken = default)
    {
        var graph = await LoadRequiredGraphAsync(projectRoot, graphRoot, cancellationToken);
        return queryService.GetMeasureLineage(graph, measureName, tableName);
    }

    [McpServerTool(Name = "report.term.search")]
    public async Task<object> SearchTermsAsync(
        string query,
        string? projectRoot = null,
        string? graphRoot = null,
        CancellationToken cancellationToken = default)
    {
        var graph = await LoadRequiredGraphAsync(projectRoot, graphRoot, cancellationToken);
        return queryService.SearchTerms(graph, query);
    }

    [McpServerTool(Name = "report.document.get")]
    public async Task<object?> GetDocumentAsync(
        string documentIdOrPath,
        string? projectRoot = null,
        string? graphRoot = null,
        CancellationToken cancellationToken = default)
    {
        var graph = await LoadRequiredGraphAsync(projectRoot, graphRoot, cancellationToken);
        return queryService.GetDocument(graph, documentIdOrPath);
    }

    [McpServerTool(Name = "report.model.table.get")]
    public async Task<object?> GetTableUsageAsync(
        string tableName,
        string? projectRoot = null,
        string? graphRoot = null,
        CancellationToken cancellationToken = default)
    {
        var graph = await LoadRequiredGraphAsync(projectRoot, graphRoot, cancellationToken);
        return queryService.GetTableUsage(graph, tableName);
    }

    [McpServerTool(Name = "report.visual.get")]
    public async Task<object?> GetVisualAsync(
        string pageId,
        string visualId,
        string? projectRoot = null,
        string? graphRoot = null,
        CancellationToken cancellationToken = default)
    {
        var graph = await LoadRequiredGraphAsync(projectRoot, graphRoot, cancellationToken);
        return queryService.GetVisual(graph, pageId, visualId);
    }

    [McpServerTool(Name = "report.graph.explore")]
    public async Task<object> ExploreAsync(
        string mode,
        string key,
        string? projectRoot = null,
        string? graphRoot = null,
        CancellationToken cancellationToken = default)
    {
        var graph = await LoadRequiredGraphAsync(projectRoot, graphRoot, cancellationToken);
        var exploreMode = mode.ToLowerInvariant() switch
        {
            "from-page" => ExploreMode.FromPage,
            "from-table" => ExploreMode.FromTable,
            _ => throw new ArgumentException("Explore mode must be 'from-page' or 'from-table'.")
        };

        return queryService.Explore(graph, new ExploreQuery(exploreMode, key));
    }

    private async Task<Core.Models.ReportGraph> LoadRequiredGraphAsync(
        string? projectRoot,
        string? graphRoot,
        CancellationToken cancellationToken)
    {
        var input = await LoadBuildInputAsync(projectRoot, graphRoot, cancellationToken);
        var resolved = await graphService.LoadOrRefreshAsync(input, cancellationToken);
        return resolved.Graph;
    }

    private async Task<Core.Models.ReportGraphBuildInput> LoadBuildInputAsync(
        string? projectRoot,
        string? graphRoot,
        CancellationToken cancellationToken)
    {
        var resolvedProjectRoot = ResolveProjectRoot(projectRoot, graphRoot);
        return await buildInputAdapter.LoadAsync(resolvedProjectRoot, cancellationToken);
    }

    private static string ResolveProjectRoot(string? projectRoot, string? graphRoot)
    {
        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            return Path.GetFullPath(projectRoot);
        }

        if (!string.IsNullOrWhiteSpace(graphRoot))
        {
            var fullGraphRoot = Path.GetFullPath(graphRoot);
            if (File.Exists(fullGraphRoot))
            {
                fullGraphRoot = Path.GetDirectoryName(fullGraphRoot)
                    ?? throw new DirectoryNotFoundException($"Could not resolve directory from graphRoot '{graphRoot}'.");
            }

            if (string.Equals(new DirectoryInfo(fullGraphRoot).Name, "Graph", StringComparison.OrdinalIgnoreCase))
            {
                return Directory.GetParent(fullGraphRoot)?.FullName
                    ?? throw new DirectoryNotFoundException($"Could not resolve project root from graphRoot '{graphRoot}'.");
            }

            return fullGraphRoot;
        }

        return Directory.GetCurrentDirectory();
    }
}
