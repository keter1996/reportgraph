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
    Task<GraphModel> RefreshAsync(ReportGraphBuildInput input, CancellationToken cancellationToken = default);
    Task<GraphModel> RefreshIfStaleAsync(ReportGraphBuildInput input, CancellationToken cancellationToken = default);
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

        return graph;
    }

    public async Task<GraphModel> RefreshIfStaleAsync(ReportGraphBuildInput input, CancellationToken cancellationToken = default)
    {
        var manifest = await fileStore.LoadManifestAsync(input.Source.PbipProjectPath, cancellationToken);
        var currentModelFingerprint = CreateModelFingerprint(input);
        var currentReportFingerprint = CreateReportFingerprint(input);

        if (manifest is null || stalenessChecker.IsStale(manifest, currentModelFingerprint, currentReportFingerprint))
        {
            return await RefreshAsync(input, cancellationToken);
        }

        var graph = await fileStore.LoadGraphAsync(input.Source.PbipProjectPath, cancellationToken);
        if (graph is not null)
        {
            return graph;
        }

        return await RefreshAsync(input, cancellationToken);
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
            IsStale: false);
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
}
