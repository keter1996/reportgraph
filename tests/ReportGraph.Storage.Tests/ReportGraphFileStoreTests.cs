using ReportGraph.Core.Models;
using ReportGraph.Storage.Artifacts;
using ReportGraph.Storage.Serialization;
using ReportGraph.Storage.Storage;
using GraphManifest = ReportGraph.Core.Models.ReportGraphManifest;
using GraphModel = ReportGraph.Core.Models.ReportGraph;

namespace ReportGraph.Storage.Tests;

public sealed class ReportGraphFileStoreTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), "ReportGraphTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Resolve_BuildsExpectedPaths()
    {
        var pbipPath = Path.Combine(tempRoot, "Project");

        var paths = ReportGraphPathResolver.Resolve(pbipPath);

        Assert.Equal(Path.Combine(pbipPath, "Graph"), paths.GraphDirectoryPath);
        Assert.Equal(Path.Combine(pbipPath, "Graph", "context"), paths.ContextDirectoryPath);
        Assert.Equal(Path.Combine(pbipPath, "Graph", "context", "pages"), paths.PagesDirectoryPath);
        Assert.Equal(Path.Combine(pbipPath, "Graph", "report-graph.json"), paths.ReportGraphFilePath);
        Assert.Equal(Path.Combine(pbipPath, "Graph", "manifest.json"), paths.ManifestFilePath);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsGraphAndManifest()
    {
        var store = new ReportGraphFileStore();
        var pbipPath = Path.Combine(tempRoot, "RoundTripProject");
        var graph = CreateGraph();
        var manifest = CreateManifest(pbipPath);

        await store.SaveGraphAsync(pbipPath, graph);
        await store.SaveManifestAsync(pbipPath, manifest);

        var loadedGraph = await store.LoadGraphAsync(pbipPath);
        var loadedManifest = await store.LoadManifestAsync(pbipPath);

        Assert.NotNull(loadedGraph);
        Assert.NotNull(loadedManifest);
        Assert.Equal(ReportGraphJson.Serialize(graph), ReportGraphJson.Serialize(loadedGraph));
        Assert.Equal(ReportGraphJson.Serialize(manifest), ReportGraphJson.Serialize(loadedManifest));
    }

    [Fact]
    public async Task SaveGraph_Twice_ProducesStableOutput()
    {
        var store = new ReportGraphFileStore();
        var pbipPath = Path.Combine(tempRoot, "StableProject");
        var graph = CreateGraph();

        await store.SaveGraphAsync(pbipPath, graph);
        var paths = store.GetPaths(pbipPath);
        var firstWrite = await File.ReadAllTextAsync(paths.ReportGraphFilePath);

        await store.SaveGraphAsync(pbipPath, graph);
        var secondWrite = await File.ReadAllTextAsync(paths.ReportGraphFilePath);

        Assert.Equal(firstWrite, secondWrite);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static GraphModel CreateGraph()
    {
        return new GraphModel(
            Version: "1.0",
            GeneratedAtUtc: new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero),
            Source: new ReportGraphSource(
                InstanceId: "instance-1",
                PbipProjectPath: @"D:\Example\Project.pbip",
                ReportRootPath: @"D:\Example\Report",
                ModelName: "Sales Model"),
            Report: new ReportGraphReportLayer(
                ReportName: "Sales",
                ActivePageId: "ReportSection1",
                Pages:
                [
                    new ReportGraphPageNode(
                        PageId: "ReportSection1",
                        DisplayName: "Overview",
                        Ordinal: 0,
                        StoryRole: StoryRole.Overview,
                        DominantVisualTypes: ["card"],
                        FocusTables: ["FactSales"],
                        FocusMeasures: ["Sales Amount"],
                        BusinessQuestion: "How are sales performing?",
                        NarrativeSummary: "High-level sales summary.")
                ],
                Storyline:
                [
                    new ReportGraphStoryStep(
                        StepId: "step-1",
                        PageId: "ReportSection1",
                        Ordinal: 0,
                        StoryRole: StoryRole.Overview,
                        Summary: "Start from the overall KPI view.")
                ]),
            Model: new ReportGraphModelLayer(
                Summary: new ReportGraphModelOverview(
                    ModelName: "Sales Model",
                    TableCount: 1,
                    ColumnCount: 1,
                    MeasureCount: 1,
                    RelationshipCount: 0),
                Tables:
                [
                    new ReportGraphTableNode(
                        Name: "FactSales",
                        SemanticRole: SemanticRole.Fact,
                        IsHidden: false,
                        ColumnCount: 1,
                        MeasureCount: 1,
                        RelationshipDegree: 0,
                        UsedByVisualCount: 1,
                        UsedByPages: ["ReportSection1"],
                        Measures: ["Sales Amount"])
                ],
                Relationships: []),
            Bindings: new ReportGraphBindingLayer(
                PageToTables:
                [
                    new ReportGraphPageTableBinding(
                        PageId: "ReportSection1",
                        Tables: ["FactSales"])
                ],
                PageToMeasures:
                [
                    new ReportGraphPageMeasureBinding(
                        PageId: "ReportSection1",
                        Measures: ["Sales Amount"])
                ],
                VisualToFields:
                [
                    new ReportGraphVisualFieldBinding(
                        PageId: "ReportSection1",
                        VisualId: "Visual1",
                        VisualType: "card",
                        Fields:
                        [
                            new ReportGraphFieldReference(
                                Role: "Value",
                                Table: "FactSales",
                                Field: "Sales Amount",
                                Kind: FieldReferenceKind.Measure)
                        ])
                ]),
            Diagnostics: new ReportGraphDiagnostics(
                Warnings: [],
                Notes: ["Storage round-trip test."]));
    }

    private static GraphManifest CreateManifest(string pbipPath)
    {
        return new GraphManifest(
            Version: "1.0",
            GraphBuilderVersion: "0.1.0",
            GeneratedAtUtc: new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero),
            PbipProjectPath: pbipPath,
            ReportRootPath: Path.Combine(pbipPath, "Report"),
            ModelFingerprint: "sales|1|1|1|0",
            ReportFingerprint: "pages|1|1",
            IsStale: false);
    }
}
