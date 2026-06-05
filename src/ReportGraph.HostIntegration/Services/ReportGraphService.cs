using ReportGraph.Core.Models;
using ReportGraph.Core.Services;
using ReportGraph.Storage.Storage;
using GraphManifest = ReportGraph.Core.Models.ReportGraphManifest;
using GraphModel = ReportGraph.Core.Models.ReportGraph;

namespace ReportGraph.HostIntegration.Services;

public interface IReportGraphService
{
    Task<GraphModel> BuildAsync(ReportGraphBuildInput input, CancellationToken cancellationToken = default);
    Task<GraphModel?> LoadAsync(string pbipProjectPath, CancellationToken cancellationToken = default);
    Task<ReportGraphResolvedGraph> LoadOrRefreshAsync(ReportGraphBuildInput input, CancellationToken cancellationToken = default);
    Task<GraphModel> RefreshAsync(ReportGraphBuildInput input, CancellationToken cancellationToken = default);
    Task<GraphModel> RefreshIfStaleAsync(ReportGraphBuildInput input, CancellationToken cancellationToken = default);
    Task<ReportGraphRefreshState> EvaluateRefreshStateAsync(ReportGraphBuildInput input, CancellationToken cancellationToken = default);
    Task MarkDirtyAsync(string pbipProjectPath, string reason, CancellationToken cancellationToken = default);
    Task DeleteAsync(string pbipProjectPath, CancellationToken cancellationToken = default);
}

public sealed class ReportGraphService : IReportGraphService
{
    private readonly IReportGraphBuilder builder;
    private readonly IReportGraphContextRenderer renderer;
    private readonly IReportGraphFingerprintService fingerprintService;
    private readonly IReportGraphStalenessChecker stalenessChecker;
    private readonly IReportGraphFileStore fileStore;
    private readonly IReportGraphContextFileStore contextFileStore;

    public ReportGraphService(
        IReportGraphBuilder builder,
        IReportGraphContextRenderer renderer,
        IReportGraphFingerprintService fingerprintService,
        IReportGraphStalenessChecker stalenessChecker,
        IReportGraphFileStore fileStore,
        IReportGraphContextFileStore contextFileStore)
    {
        this.builder = builder;
        this.renderer = renderer;
        this.fingerprintService = fingerprintService;
        this.stalenessChecker = stalenessChecker;
        this.fileStore = fileStore;
        this.contextFileStore = contextFileStore;
    }

    public Task<GraphModel> BuildAsync(ReportGraphBuildInput input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(builder.Build(input));
    }

    public Task<GraphModel?> LoadAsync(string pbipProjectPath, CancellationToken cancellationToken = default)
    {
        return fileStore.LoadGraphAsync(pbipProjectPath, cancellationToken);
    }

    public async Task<GraphModel> RefreshAsync(ReportGraphBuildInput input, CancellationToken cancellationToken = default)
    {
        var graph = builder.Build(input);
        var manifest = CreateManifest(input);
        var context = renderer.Render(graph);

        await fileStore.SaveGraphAsync(input.Source.PbipProjectPath, graph, cancellationToken);
        await fileStore.SaveManifestAsync(input.Source.PbipProjectPath, manifest, cancellationToken);
        await contextFileStore.SaveContextAsync(input.Source.PbipProjectPath, context, cancellationToken);
        await fileStore.DeleteDirtyStateAsync(input.Source.PbipProjectPath, cancellationToken);

        return graph;
    }

    public async Task<GraphModel> RefreshIfStaleAsync(ReportGraphBuildInput input, CancellationToken cancellationToken = default)
    {
        var resolved = await LoadOrRefreshAsync(input, cancellationToken);
        return resolved.Graph;
    }

    public async Task<ReportGraphRefreshState> EvaluateRefreshStateAsync(ReportGraphBuildInput input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var paths = fileStore.GetPaths(input.Source.PbipProjectPath);
        var graphDirectoryExists = Directory.Exists(paths.GraphDirectoryPath);
        var graphFileExists = File.Exists(paths.ReportGraphFilePath);
        var manifestExists = File.Exists(paths.ManifestFilePath);
        var currentModelFingerprint = CreateModelFingerprint(input);
        var currentReportFingerprint = CreateReportFingerprint(input);
        var currentSourceFingerprint = CreateSourceFingerprint(input);
        var sourceFileCount = input.SourceFiles?.Count ?? 0;
        var manifest = await fileStore.LoadManifestAsync(input.Source.PbipProjectPath, cancellationToken);
        var dirtyState = await fileStore.LoadDirtyStateAsync(input.Source.PbipProjectPath, cancellationToken);

        if (manifest is null)
        {
            return new ReportGraphRefreshState(
                GraphDirectoryExists: graphDirectoryExists,
                GraphFileExists: graphFileExists,
                ManifestExists: manifestExists,
                IsStale: true,
                Reason: "Manifest missing",
                SourceFingerprint: currentSourceFingerprint,
                SourceFileCount: sourceFileCount,
                Manifest: null,
                DirtyState: dirtyState);
        }

        if (!graphFileExists)
        {
            return new ReportGraphRefreshState(
                GraphDirectoryExists: graphDirectoryExists,
                GraphFileExists: false,
                ManifestExists: manifestExists,
                IsStale: true,
                Reason: "Graph file missing",
                SourceFingerprint: currentSourceFingerprint,
                SourceFileCount: sourceFileCount,
                Manifest: manifest,
                DirtyState: dirtyState);
        }

        if (dirtyState is not null)
        {
            return new ReportGraphRefreshState(
                GraphDirectoryExists: graphDirectoryExists,
                GraphFileExists: graphFileExists,
                ManifestExists: manifestExists,
                IsStale: true,
                Reason: dirtyState.Reason,
                SourceFingerprint: currentSourceFingerprint,
                SourceFileCount: sourceFileCount,
                Manifest: manifest,
                DirtyState: dirtyState);
        }

        var staleness = stalenessChecker.Evaluate(
            manifest,
            currentSourceFingerprint,
            currentModelFingerprint,
            currentReportFingerprint);

        return new ReportGraphRefreshState(
            GraphDirectoryExists: graphDirectoryExists,
            GraphFileExists: graphFileExists,
            ManifestExists: manifestExists,
            IsStale: staleness.IsStale,
            Reason: staleness.Reason ?? "Up to date",
            SourceFingerprint: currentSourceFingerprint,
            SourceFileCount: sourceFileCount,
            Manifest: manifest,
            DirtyState: dirtyState);
    }

    public async Task<ReportGraphResolvedGraph> LoadOrRefreshAsync(ReportGraphBuildInput input, CancellationToken cancellationToken = default)
    {
        var refreshState = await EvaluateRefreshStateAsync(input, cancellationToken);
        if (refreshState.IsStale)
        {
            var refreshedGraph = await RefreshAsync(input, cancellationToken);
            return new ReportGraphResolvedGraph(
                Graph: refreshedGraph,
                RefreshState: refreshState,
                WasRefreshed: true);
        }

        var graph = await fileStore.LoadGraphAsync(input.Source.PbipProjectPath, cancellationToken);
        if (graph is not null)
        {
            return new ReportGraphResolvedGraph(
                Graph: graph,
                RefreshState: refreshState,
                WasRefreshed: false);
        }

        var fallbackGraph = await RefreshAsync(input, cancellationToken);
        return new ReportGraphResolvedGraph(
            Graph: fallbackGraph,
            RefreshState: refreshState with { IsStale = true, Reason = "Graph file missing" },
            WasRefreshed: true);
    }

    public Task MarkDirtyAsync(string pbipProjectPath, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pbipProjectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return fileStore.SaveDirtyStateAsync(
            pbipProjectPath,
            new ReportGraphDirtyState(reason, DateTimeOffset.UtcNow),
            cancellationToken);
    }

    public Task DeleteAsync(string pbipProjectPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var paths = fileStore.GetPaths(pbipProjectPath);
        if (Directory.Exists(paths.GraphDirectoryPath))
        {
            Directory.Delete(paths.GraphDirectoryPath, recursive: true);
        }

        return Task.CompletedTask;
    }

    private GraphManifest CreateManifest(ReportGraphBuildInput input)
    {
        return new GraphManifest(
            Version: input.Version,
            GraphBuilderVersion: "0.1.0",
            GeneratedAtUtc: input.GeneratedAtUtc,
            PbipProjectPath: input.Source.PbipProjectPath,
            ReportRootPath: input.Source.ReportRootPath,
            ModelFingerprint: CreateModelFingerprint(input),
            ReportFingerprint: CreateReportFingerprint(input),
            IsStale: false,
            SourceFingerprint: CreateSourceFingerprint(input),
            SourceFiles: input.SourceFiles,
            StaleReason: null);
    }

    private string CreateModelFingerprint(ReportGraphBuildInput input)
    {
        return fingerprintService.CreateModelFingerprint(
            new ModelFingerprintInput(
                ModelName: input.Model.ModelName ?? "unknown-model",
                TableCount: input.Model.Tables.Count,
                ColumnCount: input.Model.Tables.Sum(table => table.Columns.Count),
                MeasureCount: input.Model.Tables.Sum(table => table.Measures.Count),
                RelationshipCount: input.Model.Relationships.Count));
    }

    private string CreateReportFingerprint(ReportGraphBuildInput input)
    {
        return fingerprintService.CreateReportFingerprint(
            new ReportFingerprintInput(
                PagesLastWriteUtc: input.Report.PagesLastModifiedUtc,
                PageCount: input.Report.Pages.Count,
                VisualCount: input.Report.Pages.Sum(page => page.Visuals.Count)));
    }

    private string? CreateSourceFingerprint(ReportGraphBuildInput input)
    {
        return fingerprintService.CreateSourceFingerprint(input.SourceFiles);
    }
}

public sealed record ReportGraphRefreshState(
    bool GraphDirectoryExists,
    bool GraphFileExists,
    bool ManifestExists,
    bool IsStale,
    string Reason,
    string? SourceFingerprint,
    int SourceFileCount,
    GraphManifest? Manifest,
    ReportGraphDirtyState? DirtyState);

public sealed record ReportGraphResolvedGraph(
    GraphModel Graph,
    ReportGraphRefreshState RefreshState,
    bool WasRefreshed);
