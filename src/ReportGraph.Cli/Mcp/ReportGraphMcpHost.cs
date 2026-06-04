using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ReportGraph.Query.Services;
using ReportGraph.Storage.Storage;

namespace ReportGraph.Cli.Mcp;

internal static class ReportGraphMcpHost
{
    public static async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<IReportGraphQueryService, ReportGraphQueryService>();
        builder.Services.AddSingleton<IReportGraphFileStore, ReportGraphFileStore>();
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
    private readonly IReportGraphQueryService queryService;
    private readonly IReportGraphFileStore fileStore;

    public ReportGraphMcpTools(
        IReportGraphQueryService queryService,
        IReportGraphFileStore fileStore)
    {
        this.queryService = queryService;
        this.fileStore = fileStore;
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
        var resolvedProjectRoot = ResolveProjectRoot(projectRoot, graphRoot);
        var graph = await fileStore.LoadGraphAsync(resolvedProjectRoot, cancellationToken);
        if (graph is null)
        {
            throw new InvalidOperationException(
                $"No graph artifacts were found under '{resolvedProjectRoot}'. Run reportgraph init or reportgraph update first.");
        }

        return graph;
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
